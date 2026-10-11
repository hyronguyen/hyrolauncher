using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace HYROLauncherWeb
{
    internal static class PetAnimationTiming
    {
        public static int Normalize(int milliseconds)
        {
            return milliseconds<=0 ? 0 : Math.Max(20,((Math.Min(2000,milliseconds)+5)/10)*10);
        }

        public static string PlaybackPath(string source,int milliseconds,string cacheDirectory)
        {
            milliseconds=Normalize(milliseconds);
            if(milliseconds==0 || String.IsNullOrWhiteSpace(source) || !File.Exists(source)) return source;
            var info=new FileInfo(source);
            if(info.Length>20*1024*1024) throw new InvalidDataException("GIF is too large.");
            byte[] original=File.ReadAllBytes(source);
            string hash;
            using(var sha=SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(original)).Replace("-","").ToLowerInvariant();
            string target=Path.Combine(cacheDirectory,hash+"-"+milliseconds+".gif");
            if(File.Exists(target)) return target;
            byte[] timed=Retime(original,milliseconds);
            Directory.CreateDirectory(cacheDirectory);
            File.WriteAllBytes(target,timed);
            return target;
        }

        public static byte[] Retime(byte[] source,int milliseconds)
        {
            int delay=Normalize(milliseconds)/10;
            if(delay==0) return (byte[])source.Clone();
            byte[] bytes=(byte[])source.Clone();
            if(bytes.Length<14 || bytes[0]!='G'||bytes[1]!='I'||bytes[2]!='F'||bytes[3]!='8'||(bytes[4]!='7'&&bytes[4]!='9')||bytes[5]!='a')
                throw new InvalidDataException("Invalid GIF header.");
            bytes[4]=(byte)'9'; // Graphic-control extensions require GIF89a.
            int offset=13;
            if((bytes[10]&128)!=0) offset+=3*(1<<((bytes[10]&7)+1));
            Require(bytes,offset);
            using(var output=new MemoryStream())
            {
                output.Write(bytes,0,offset);
                bool frameControl=false;
                int frames=0;
                while(offset<bytes.Length)
                {
                    int start=offset;
                    int block=bytes[offset++];
                    if(block==0x3B)
                    {
                        if(frames==0) throw new InvalidDataException("GIF has no frames.");
                        output.Write(bytes,start,bytes.Length-start);
                        return output.ToArray();
                    }
                    if(block==0x21)
                    {
                        Require(bytes,offset+1);
                        int label=bytes[offset++];
                        if(label==0xF9)
                        {
                            Require(bytes,offset+6);
                            if(bytes[offset]!=4 || bytes[offset+5]!=0) throw new InvalidDataException("Invalid GIF frame control.");
                            bytes[offset+2]=(byte)(delay&255);
                            bytes[offset+3]=(byte)(delay>>8);
                            frameControl=true;
                        }
                        SkipSubBlocks(bytes,ref offset);
                        if(label==0x01) frameControl=false;
                    }
                    else if(block==0x2C)
                    {
                        Require(bytes,offset+9);
                        if(!frameControl)
                        {
                            byte[] control={0x21,0xF9,4,0,(byte)(delay&255),(byte)(delay>>8),0,0};
                            output.Write(control,0,control.Length);
                        }
                        int packed=bytes[offset+8];
                        offset+=9;
                        if((packed&128)!=0) offset+=3*(1<<((packed&7)+1));
                        Require(bytes,offset+1);
                        offset++; // LZW minimum code size; compressed pixels stay unchanged.
                        SkipSubBlocks(bytes,ref offset);
                        frameControl=false;
                        frames++;
                    }
                    else throw new InvalidDataException("Unsupported GIF block.");
                    output.Write(bytes,start,offset-start);
                }
                throw new InvalidDataException("Incomplete GIF.");
            }
        }

        private static void Require(byte[] bytes,int end)
        {
            if(end>bytes.Length) throw new InvalidDataException("Truncated GIF.");
        }
        private static void SkipSubBlocks(byte[] bytes,ref int offset)
        {
            while(true)
            {
                Require(bytes,offset+1);
                int count=bytes[offset++];
                if(count==0) return;
                Require(bytes,offset+count);
                offset+=count;
            }
        }
    }

    internal partial class WebLauncherForm
    {
        private static string PetTimingCacheDirectory
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cache","pet-timing"); }
        }
        private static string PetPlaybackPath(string path,int delay)
        {
            path=ResolveStoredAssetPath(path);
            try { return PetAnimationTiming.PlaybackPath(path,delay,PetTimingCacheDirectory); }
            catch { return path; }
        }
        private static void PreparePetPlayback(LauncherState snapshot)
        {
            foreach(PluginEntry plugin in snapshot.Plugins ?? new List<PluginEntry>())
            {
                if(!String.Equals(plugin.Type,"pet",StringComparison.OrdinalIgnoreCase)) continue;
                foreach(PetDefinition pet in plugin.Pets ?? new List<PetDefinition>())
                {
                    pet.IdlePlaybackPath=PetPlaybackPath(pet.IdleGifPath,plugin.PetIdleFrameDelayMs ?? plugin.PetFrameDelayMs);
                    pet.DragPlaybackPath=PetPlaybackPath(String.IsNullOrWhiteSpace(pet.DragGifPath)?pet.IdleGifPath:pet.DragGifPath,plugin.PetDragFrameDelayMs ?? plugin.PetFrameDelayMs);
                }
            }
        }
        private void PreviewPetTiming(Dictionary<string,object> message)
        {
            if(pluginWindow==null||pluginWindow.IsDisposed||currentBuiltInPluginId!=BuiltInPetPluginId) return;
            int delay=PetAnimationTiming.Normalize(ClampInt(GetString(message,"delayMs"),0,0,2000));
            int idleDelay=PetAnimationTiming.Normalize(ClampInt(GetString(message,"idleDelayMs"),delay,0,2000));
            int dragDelay=PetAnimationTiming.Normalize(ClampInt(GetString(message,"dragDelayMs"),delay,0,2000));
            string idle=GetString(message,"idlePath"),drag=GetString(message,"dragPath");
            pluginWindow.Post(new Dictionary<string,object> {
                {"type","petTimingPreview"},{"requestId",GetString(message,"requestId")},
                {"idlePath",PetPlaybackPath(idle,idleDelay)},{"dragPath",PetPlaybackPath(String.IsNullOrWhiteSpace(drag)?idle:drag,dragDelay)}
            });
        }
    }
}
