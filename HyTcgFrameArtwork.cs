using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace HYROLauncherWeb
{
    internal sealed partial class HyTcgEconomy
    {
        // Only a quick local lookup is called while holding the economy lock.
        // Network repairs run separately before the protected image is composed.
        internal Func<HyTcgCard,string> DefaultArtworkLookup;
        private string DefaultArtwork(HyTcgCard card,bool positioned=false)
        {
            if(DefaultArtworkLookup==null || (!positioned && !HyTcgCosmeticRenderer.NeedsDefaultRecomposition(card)))return null;
            try{return DefaultArtworkLookup(card);}catch{return null;}
        }
    }

    internal sealed partial class HyTcgService
    {
        private readonly object frameArtworkLock=new object();
        private readonly SemaphoreSlim frameArtworkGate=new SemaphoreSlim(1,1);
        private readonly Dictionary<string,string> frameArtworkMemory=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly Queue<string> frameArtworkOrder=new Queue<string>();
        private readonly Dictionary<string,DateTime> frameArtworkAttempts=new Dictionary<string,DateTime>(StringComparer.Ordinal);
        private int frameArtworkBytes;
        private string FrameArtworkDirectory {get{return Path.Combine(root,".cache","hytcg-frame-artwork");}}
        private static string FrameArtworkKey(HyTcgCard card)
        {
            if(card==null || card.CharacterId<1 || !HyTcgTransport.AllowedImage(card.SourceImage))return null;
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(card.CharacterId+"\n"+card.SourceImage))).Replace("-","").ToLowerInvariant();
        }
        private bool SafeFrameArtworkDirectory(bool create)
        {
            // Never follow a junction introduced below or above the cache folder.
            for(var parent=new DirectoryInfo(FrameArtworkDirectory);parent!=null;parent=parent.Parent)
                if(parent.Exists && (parent.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            if(create)Directory.CreateDirectory(FrameArtworkDirectory);
            return Directory.Exists(FrameArtworkDirectory);
        }
        private static byte[] FrameArtworkEntropy(string key) {return Encoding.UTF8.GetBytes("HYTCG original frame artwork v2\n"+key);}
        private void RememberFrameArtwork(string key,string data)
        {
            if(frameArtworkMemory.ContainsKey(key))return;
            frameArtworkMemory[key]=data;frameArtworkOrder.Enqueue(key);frameArtworkBytes+=data.Length*2;
            while(frameArtworkMemory.Count>24 || frameArtworkBytes>16*1024*1024)
            {string old=frameArtworkOrder.Dequeue();frameArtworkBytes-=frameArtworkMemory[old].Length*2;frameArtworkMemory.Remove(old);}
        }
        internal string CachedDefaultArtwork(HyTcgCard card)
        {
            string key=FrameArtworkKey(card);if(key==null)return null;
            lock(frameArtworkLock)
            {
                string data;if(frameArtworkMemory.TryGetValue(key,out data))return data;
                try
                {
                    string file=Path.Combine(FrameArtworkDirectory,key+".bin");
                    if(!SafeFrameArtworkDirectory(false) || !File.Exists(file) || (File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0 || new FileInfo(file).Length>4*1024*1024+4096)return null;
                    byte[] png=ProtectedData.Unprotect(File.ReadAllBytes(file),FrameArtworkEntropy(key),DataProtectionScope.CurrentUser);
                    int width,height;png=HyTcgEconomy.NormalizeCustomArtwork(png,out width,out height,false);
                    data="data:image/png;base64,"+Convert.ToBase64String(png);RememberFrameArtwork(key,data);return data;
                }
                catch{return null;}
            }
        }
        internal void WarmDefaultArtwork(HyTcgCard card,CancellationToken token)
        {WarmDefaultArtwork(card,token,false);}
        internal void WarmDefaultArtwork(HyTcgCard card,CancellationToken token,bool positioned)
        {
            if((!positioned && !HyTcgCosmeticRenderer.NeedsDefaultRecomposition(card)) || CachedDefaultArtwork(card)!=null)return;
            string marker;
            // Headerless v2 already contains the complete fitted artwork window;
            // border-only updates can reuse those local pixels without another API call.
            if(!positioned && card.Metadata!=null && card.Metadata.TryGetValue("hytcg_card_layout",out marker)
                && (marker=="window-v2" || marker=="window-v3" || marker=="window-v4"))return;
            string key=FrameArtworkKey(card);if(key==null)return;
            frameArtworkGate.Wait(token);
            try
            {
                token.ThrowIfCancellationRequested();if(CachedDefaultArtwork(card)!=null)return;
                lock(frameArtworkLock)
                {
                    DateTime attempt;
                    if(frameArtworkAttempts.TryGetValue(key,out attempt) && DateTime.UtcNow-attempt<TimeSpan.FromMinutes(5))return;
                    if(frameArtworkAttempts.Count>256)frameArtworkAttempts.Clear();frameArtworkAttempts[key]=DateTime.UtcNow;
                }
                var config=LoadConfig();string data=transport.Image(card.SourceImage,card.ImageSource=="zerochan"?"HYTCG - "+config.ZerochanUsername:"HYTCG/1.0 (HYRO Launcher)",config.RequestTimeoutSeconds,token);
                // Do not replace a missing ALT with a different profile illustration.
                // If the exact issued artwork is unavailable, retain canonical pixels.
                Match match=Regex.Match(data??"",@"^data:image/(png|jpeg);base64,([A-Za-z0-9+/=\r\n]+)$",RegexOptions.IgnoreCase);
                if(!match.Success)return;
                int width,height;byte[] png=HyTcgEconomy.NormalizeCustomArtwork(Convert.FromBase64String(match.Groups[2].Value),out width,out height,false);
                token.ThrowIfCancellationRequested();data="data:image/png;base64,"+Convert.ToBase64String(png);
                lock(frameArtworkLock)
                {
                    token.ThrowIfCancellationRequested();RememberFrameArtwork(key,data);
                    if(!SafeFrameArtworkDirectory(true))return;
                    string file=Path.Combine(FrameArtworkDirectory,key+".bin"),temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
                    try
                    {
                        byte[] bytes=ProtectedData.Protect(png,FrameArtworkEntropy(key),DataProtectionScope.CurrentUser);
                        using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
                        token.ThrowIfCancellationRequested();
                        if(File.Exists(file)){if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return;File.Replace(temporary,file,null);}else File.Move(temporary,file);
                        // This disposable display cache never changes owned card files.
                        foreach(var old in new DirectoryInfo(FrameArtworkDirectory).GetFiles("*.bin").Where(f=>(f.Attributes&FileAttributes.ReparsePoint)==0 && Regex.IsMatch(f.Name,@"^[a-f0-9]{64}\.bin$"))
                            .OrderByDescending(f=>f.LastWriteTimeUtc).Skip(128))try{old.Delete();}catch{}
                    }
                    finally{if(File.Exists(temporary))File.Delete(temporary);}
                }
            }
            catch(OperationCanceledException){throw;}
            catch(Exception error){ApiActivityLog.Note("HYTCG","Original artwork repair unavailable · #"+card.CharacterId+" · "+error.GetType().Name);}
            finally{frameArtworkGate.Release();}
        }
    }
}
