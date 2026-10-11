using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace HYROLauncherWeb
{
    public class HyTcgRate
    {
        public string Name { get; set; }
        public double Weight { get; set; }
        public string Color { get; set; }
    }
    public class HyTcgConfig
    {
        public int CharacterIdMin { get; set; }
        public int CharacterIdMax { get; set; }
        public int MaxCharacterAttempts { get; set; }
        public int Concurrency { get; set; }
        public int RequestTimeoutSeconds { get; set; }
        public int RetryDelayMs { get; set; }
        public string ZerochanUsername { get; set; }
        public List<HyTcgRate> DropRates { get { return HyTcgRules.DropRates(); } }
    }
    public class HyTcgCard
    {
        public int CharacterId { get; set; }
        public string Name { get; set; }
        public string Series { get; set; }
        public string Gender { get; set; }
        public string Rarity { get; set; }
        public string RarityColor { get; set; }
        public string SourceImage { get; set; }
        public string ProfileImage { get; set; }
        public string ImageSource { get; set; }
        public string ArtworkPage { get; set; }
        public string OriginalSource { get; set; }
        public string FallbackReason { get; set; }
        public string CreatedAt { get; set; }
        public string CardId { get; set; }
        public string Orientation { get; set; }
        public int ArtworkWidth { get; set; }
        public int ArtworkHeight { get; set; }
        public int Revision { get; set; }
        public Dictionary<string,string> Metadata { get; set; }
    }
    public class HyTcgResult
    {
        public HyTcgCard Card { get; set; }
        public string ImageData { get; set; }
    }
    public class HyTcgCollectionItem
    {
        public HyTcgCard Card { get; set; }
        public string FileName { get; set; }
        public string ImagePath { get; set; }
        public bool CanDissolve { get; set; }
        public int SalvageValue { get; set; }
        public bool IsLegacy { get; set; }
        public bool Favorite { get; set; }
        public bool CanUpgrade { get; set; }
        public bool UpgradeLocked { get; set; }
        public HyTcgCosmetic Cosmetic { get; set; }
        public bool ExpeditionLocked { get; set; }
    }
    public class HyTcgCollectionPage
    {
        public List<HyTcgCollectionItem> Items { get; set; }
        public int Total { get; set; }
        public int Offset { get; set; }
        public int NextOffset { get; set; }
        public bool HasMore { get; set; }
    }
    internal sealed class HyTcgException : Exception
    {
        internal string Code;
        internal HyTcgException(string code) : base(code) { Code=code; }
    }
    internal interface IHyTcgTransport
    {
        string Json(string url,string userAgent,int timeout,CancellationToken token);
        string Image(string url,string userAgent,int timeout,CancellationToken token);
    }
    // Fixed API/HTTPS image allowlist. ACDB uses its documented Windows curl client.
    internal sealed class HyTcgTransport : IHyTcgTransport
    {
        internal static bool AllowedImage(string url)
        {
            Uri uri;
            if(!Uri.TryCreate(url,UriKind.Absolute,out uri) || uri.Scheme!="https" || !uri.IsDefaultPort || uri.UserInfo!="") return false;
            string host=uri.DnsSafeHost.ToLowerInvariant();
            return host=="animecharactersdatabase.com" || host.EndsWith(".animecharactersdatabase.com",StringComparison.Ordinal)
                || host=="zerochan.net" || host.EndsWith(".zerochan.net",StringComparison.Ordinal);
        }
        public string Json(string url,string userAgent,int timeout,CancellationToken token)
        {
            string contentType;
            return Encoding.UTF8.GetString(Read(url,userAgent,timeout,2*1024*1024,false,token,out contentType));
        }
        public string Image(string url,string userAgent,int timeout,CancellationToken token)
        {
            string contentType;
            byte[] bytes=Read(url,userAgent,timeout,8*1024*1024,true,token,out contentType);
            string mime="";
            if(bytes.Length>8 && bytes[0]==137 && bytes[1]==80 && bytes[2]==78 && bytes[3]==71) mime="image/png";
            else if(bytes.Length>3 && bytes[0]==255 && bytes[1]==216) mime="image/jpeg";
            else if(bytes.Length>12 && Encoding.ASCII.GetString(bytes,0,4)=="RIFF" && Encoding.ASCII.GetString(bytes,8,4)=="WEBP") mime="image/webp";
            else if(bytes.Length>6 && Encoding.ASCII.GetString(bytes,0,3)=="GIF") mime="image/gif";
            else if(bytes.Length>16 && Encoding.ASCII.GetString(bytes,4,4)=="ftyp" && Encoding.ASCII.GetString(bytes,8,Math.Min(32,bytes.Length-8)).Contains("avif")) mime="image/avif";
            if(mime=="") throw new HyTcgException("ArtworkInvalid");
            return "data:"+mime+";base64,"+Convert.ToBase64String(bytes);
        }
        private static byte[] Read(string url,string agent,int timeout,int limit,bool image,CancellationToken token,out string contentType)
        {
            contentType="";
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            for(int redirect=0;redirect<4;redirect++)
            {
                token.ThrowIfCancellationRequested();
                Uri uri;
                if(!Uri.TryCreate(url,UriKind.Absolute,out uri) || !AllowedImage(url)) throw new HyTcgException("ArtworkInvalid");
                if(!image && uri.Host!="www.zerochan.net" && uri.Host!="www.animecharactersdatabase.com") throw new HyTcgException("ApiInvalid");
                if(HyTcgAcdbClient.IsAcdb(uri)) return HyTcgAcdbClient.Read(uri,timeout,limit,image,token,out contentType);
                if(redirect>0 && !image && uri.Host=="www.zerochan.net") HyTcgService.WaitForZerochan(token);
                var request=(HttpWebRequest)WebRequest.Create(uri);
                request.UserAgent=agent; request.Accept=image?"image/avif,image/webp,image/*":"application/json";
                request.Timeout=timeout*1000; request.ReadWriteTimeout=timeout*1000;
                request.AllowAutoRedirect=false; request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                if(uri.Host.EndsWith("zerochan.net",StringComparison.Ordinal)) request.Referer="https://www.zerochan.net/";
                using(var trace=ApiActivityLog.Begin("Zerochan",image?"IMAGE":"GET",uri.AbsoluteUri))
                {
                  try {
                  using(token.Register(delegate { request.Abort(); }))
                  using(var response=(HttpWebResponse)request.GetResponse())
                  {
                    if((int)response.StatusCode>=300 && (int)response.StatusCode<400)
                    {
                        trace.Complete((int)response.StatusCode,0);
                        Uri next;
                        if(!Uri.TryCreate(uri,response.Headers["Location"],out next) || !AllowedImage(next.AbsoluteUri)) throw new HyTcgException("ApiInvalid");
                        // Zerochan aliases redirect to a canonical tag and sometimes drop json/strict.
                        if(!image && uri.Host=="www.zerochan.net" && next.Host==uri.Host && !next.Query.Contains("json") && !next.Query.Contains("xml"))
                            next=new Uri(next.GetLeftPart(UriPartial.Path)+(uri.Query.Contains("xml")?"?xml":"?json")+"&strict&l=50");
                        url=next.AbsoluteUri; continue;
                    }
                    if(response.ContentLength>limit) throw new HyTcgException("ArtworkTooLarge");
                    contentType=response.ContentType;
                    using(var input=response.GetResponseStream())
                    using(var output=new MemoryStream())
                    {
                        byte[] buffer=new byte[16384]; int count;
                        while((count=input.Read(buffer,0,buffer.Length))>0)
                        {
                            token.ThrowIfCancellationRequested();
                            if(output.Length+count>limit) throw new HyTcgException("ArtworkTooLarge");
                            output.Write(buffer,0,count);
                        }
                        trace.Complete((int)response.StatusCode,output.Length);return output.ToArray();
                    }
                  }
                  } catch(Exception error){trace.Fail(error);throw;}
                }
            }
            throw new HyTcgException("ApiInvalid");
        }
    }

    internal sealed partial class HyTcgService
    {
        internal const string AcdbEndpoint="https://www.animecharactersdatabase.com/api_series_characters.php";
        internal static readonly string[] Rarities={"Common","Uncommon","Rare","SR","SSR","UR"};
        private readonly string root;
        private string configRoot;
        private readonly IHyTcgTransport transport;
        private HyTcgEconomy economy;
        private static readonly object zerochanLock=new object();
        private static DateTime nextZerochan=DateTime.MinValue;
        private static readonly object storageLock=new object();
        internal HyTcgService(string baseDirectory) : this(baseDirectory,new HyTcgTransport()) { }
        internal HyTcgService(string baseDirectory,IHyTcgTransport transport) : this(baseDirectory,transport,null) { }
        internal HyTcgService(string baseDirectory,IHyTcgTransport transport,HyTcgEconomy wallet)
        {
            root=Path.GetFullPath(baseDirectory);this.transport=transport;economy=wallet;
            configRoot=root;
        }
        internal void UseReadOnlyConfig(string baseDirectory) { configRoot=Path.GetFullPath(baseDirectory); }
        internal string CardsDirectory { get { return Path.Combine(root,"cards"); } }
        internal HyTcgEconomy Economy
        {
            get
            {
                lock(storageLock)
                {
                    if(economy==null)
                    {
                        var candidate=new HyTcgEconomy(root);candidate.RecoverUnused();
                        EnsureCardsDirectory();
                        if(!candidate.MigrationComplete)
                        {
                            foreach(var file in new DirectoryInfo(CardsDirectory).GetFiles("*.png"))
                            {
                                try
                                {
                                    if((file.Attributes&FileAttributes.ReparsePoint)!=0 || file.Length>16*1024*1024)continue;
                                    var card=Parser().Deserialize<HyTcgCard>(Png.Metadata(file.FullName));
                                    if(card!=null) candidate.TryRegisterLegacy(card,file.Name,File.ReadAllBytes(file.FullName));
                                }
                                catch(HyTcgException) { throw; }
                                catch { /* Invalid legacy files are not imported. */ }
                            }
                            candidate.FinishMigration();
                        }
                        economy=candidate;
                    }
                    if(!storageMigrated) { economy.MigrateEncryptedStorage();storageMigrated=true; }
                    if(economy.CharacterFavouritesLookup==null)economy.CharacterFavouritesLookup=CachedCharacterFavourites;
                    if(economy.DefaultArtworkLookup==null)economy.DefaultArtworkLookup=CachedDefaultArtwork;
                    return economy;
                }
            }
        }
        internal string ConfigPath { get { return Path.Combine(configRoot,"assets","apikey","hytcgconfig.json"); } }
        private static JavaScriptSerializer Parser() { return new JavaScriptSerializer { MaxJsonLength=3*1024*1024 }; }
        internal HyTcgConfig LoadConfig()
        {
            lock(storageLock)
            {
                string path=File.Exists(ConfigPath)?ConfigPath:File.Exists(Path.Combine(configRoot,"hytcg-config.json"))?Path.Combine(configRoot,"hytcg-config.json"):Path.Combine(configRoot,"assets","builtin-plugins","hytcg","config.json");
                try
                {
                    var config=Parser().Deserialize<HyTcgConfig>(File.ReadAllText(path));
                    string username=Path.Combine(configRoot,"assets","apikey","zerochanuser.txt");
                    config.ZerochanUsername=File.Exists(username)?File.ReadAllText(username,Encoding.UTF8).Trim():"";
                    ValidateConfig(config);
                    if(configRoot==root && !File.Exists(ConfigPath)) WriteConfig(config);
                    return config;
                }
                catch(HyTcgException) { throw; }
                catch { throw new HyTcgException("ConfigInvalid"); }
            }
        }
        internal void SaveConfig(HyTcgConfig config)
        {
            if(configRoot!=root)throw new HyTcgException("DevReadOnlyConfig");
            ValidateConfig(config);
            lock(storageLock) WriteConfig(config);
        }
        private void WriteConfig(HyTcgConfig config)
        {
            string temp=ConfigPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                var data=new Dictionary<string,object>{{"CharacterIdMin",config.CharacterIdMin},{"CharacterIdMax",config.CharacterIdMax},{"MaxCharacterAttempts",config.MaxCharacterAttempts},{"Concurrency",config.Concurrency},{"RequestTimeoutSeconds",config.RequestTimeoutSeconds},{"RetryDelayMs",config.RetryDelayMs}};
                File.WriteAllText(temp,Parser().Serialize(data),new UTF8Encoding(false));
                if(File.Exists(ConfigPath)) File.Replace(temp,ConfigPath,null);
                else File.Move(temp,ConfigPath);
            }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        internal static void ValidateConfig(HyTcgConfig config)
        {
            if(config==null || config.CharacterIdMin<1 || config.CharacterIdMax<config.CharacterIdMin || config.CharacterIdMax>1000000
                || config.MaxCharacterAttempts<1 || config.MaxCharacterAttempts>20 || config.Concurrency<1 || config.Concurrency>3
                || config.RequestTimeoutSeconds<3 || config.RequestTimeoutSeconds>20 || config.RetryDelayMs<100 || config.RetryDelayMs>3000)
                throw new HyTcgException("ConfigInvalid");
            config.ZerochanUsername=(config.ZerochanUsername??"").Trim();
            if(config.ZerochanUsername.Length>60 || !Regex.IsMatch(config.ZerochanUsername,@"^[A-Za-z0-9_. -]*$")) throw new HyTcgException("UsernameInvalid");
        }
        internal static int RandomIndex(int count)
        {
            if(count<1) throw new ArgumentOutOfRangeException("count");
            byte[] bytes=new byte[4]; uint value,limit=UInt32.MaxValue-(UInt32.MaxValue%(uint)count);
            using(var random=RandomNumberGenerator.Create()) do { random.GetBytes(bytes); value=BitConverter.ToUInt32(bytes,0); } while(value>=limit);
            return (int)(value%(uint)count);
        }
        internal static HyTcgRate PickRarity(HyTcgConfig config,double roll)
        {
            ValidateConfig(config);
            if(roll<0 || roll>=1 || Double.IsNaN(roll)) throw new ArgumentOutOfRangeException("roll");
            double threshold=0;
            foreach(var rate in config.DropRates) { threshold+=rate.Weight; if(roll*100<threshold) return rate; }
            return config.DropRates.Last(r=>r.Weight>0);
        }
        internal HyTcgResult Open(HyTcgConfig config,Action<string,int> progress,CancellationToken token)
        {
            var rarity=PickRarity(config,RandomIndex(1000000)/1000000d);
            HyTcgCard card=null; int connectionErrors=0; bool unavailable=false;
            for(int attempt=1;attempt<=config.MaxCharacterAttempts;attempt++)
            {
                token.ThrowIfCancellationRequested(); progress("character",attempt);
                int characterId=config.CharacterIdMin+RandomIndex(config.CharacterIdMax-config.CharacterIdMin+1);
                try
                {
                    string json=transport.Json(AcdbEndpoint+"?character_id="+characterId,"HYTCG/1.0 (HYRO Launcher)",config.RequestTimeoutSeconds,token);
                    if(HyTcgAcdbClient.IsBrowserCheck(json)) throw new HyTcgException("AcdbBrowserCheck");
                    card=ParseCharacter(json,characterId);
                    ApiActivityLog.Note("HYTCG","ACDB #"+characterId+" · "+(card==null?"Invalid / missing character":card.Name+" / "+card.Series));
                    connectionErrors=0;
                    if(card!=null) break;
                }
                catch(WebException ex)
                {
                    token.ThrowIfCancellationRequested();
                    var response=ex.Response as HttpWebResponse;
                    var nativeStatus=ex as HyTcgHttpException;
                    int status=nativeStatus!=null?nativeStatus.StatusCode:response==null?0:(int)response.StatusCode;
                    if(response!=null) response.Dispose();
                    if(status==429) throw new HyTcgException("AcdbRateLimited");
                    if(status==403) throw new HyTcgException("AcdbUnavailable");
                    if(status!=404) { unavailable=true; if(++connectionErrors>=3) throw new HyTcgException("AcdbUnavailable"); }
                }
                catch(OperationCanceledException) { throw; }
                catch(HyTcgException) { throw; }
                catch { /* Invalid JSON / missing ID is retried, never fabricated. */ }
                if(attempt<config.MaxCharacterAttempts) Wait(config.RetryDelayMs,token);
            }
            if(card==null) throw new HyTcgException(unavailable?"AcdbUnavailable":"CharacterNotFound");
            card.Rarity=rarity.Name; card.RarityColor=rarity.Color;
            card.CreatedAt=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);
            card.CardId="HT-"+DateTime.UtcNow.ToString("yyyyMMdd")+"-"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant();
            card.ImageSource="acdb"; card.SourceImage=card.ProfileImage;
            string data="";
            if(!String.IsNullOrWhiteSpace(config.ZerochanUsername))
            {
                progress("artwork",0);
                try
                {
                    string agent="HYTCG - "+config.ZerochanUsername;
                    var detail=FindArtwork(card,agent,config,token);
                    if(detail!=null)
                    {
                        int entryId=Number(detail,"id");
                        string image=First(detail,"full","medium","large","thumbnail");
                        if(!HyTcgTransport.AllowedImage(image)) throw new HyTcgException("ArtworkInvalid");
                        data=transport.Image(image,agent,config.RequestTimeoutSeconds,token);
                        card.ImageSource="zerochan"; card.SourceImage=image;
                        card.ArtworkPage="https://www.zerochan.net/"+entryId;
                        card.OriginalSource=First(detail,"source");
                    }
                    else card.FallbackReason="ZerochanEmpty";
                }
                catch(OperationCanceledException) { throw; }
                catch(Exception error) { card.FallbackReason="ZerochanUnavailable";ApiActivityLog.Note("HYTCG", "Artwork unavailable for #"+card.CharacterId+" / "+card.Name+" · "+error.GetType().Name); }
            }
            else card.FallbackReason="ZerochanUsernameMissing";
            if(data=="")
            {
                progress("profile",0);
                try { data=transport.Image(card.ProfileImage,"HYTCG/1.0 (HYRO Launcher)",config.RequestTimeoutSeconds,token); }
                catch(HyTcgException ex) { token.ThrowIfCancellationRequested(); if(ex.Code=="AcdbBrowserCheck" || ex.Code=="AcdbClientUnavailable" || ex.Code=="AcdbRateLimited") throw; throw new HyTcgException("ProfileUnavailable"); }
                catch { token.ThrowIfCancellationRequested(); throw new HyTcgException("ProfileUnavailable"); }
            }
            token.ThrowIfCancellationRequested();
            SetArtworkShape(card,data);
            ApiActivityLog.Note("HYTCG","#"+card.CharacterId+" · "+card.Name+" · "+card.Series+" → "+(card.ImageSource=="zerochan"?"ALT":"Profile · "+card.FallbackReason));
            return new HyTcgResult { Card=card,ImageData=data };
        }
        internal string Fallback(HyTcgCard card,HyTcgConfig config,CancellationToken token)
        {
            string data=transport.Image(card.ProfileImage,"HYTCG/1.0 (HYRO Launcher)",config.RequestTimeoutSeconds,token);
            card.ImageSource="acdb"; card.SourceImage=card.ProfileImage; card.ArtworkPage=""; card.OriginalSource=""; card.FallbackReason="ZerochanImageInvalid";
            SetArtworkShape(card,data);
            return data;
        }
        internal HyTcgResult Resume(HyTcgCard card,HyTcgConfig config,CancellationToken token)
        {
            try
            {
                string data=transport.Image(card.SourceImage,card.ImageSource=="zerochan"?"HYTCG - "+config.ZerochanUsername:"HYTCG/1.0 (HYRO Launcher)",config.RequestTimeoutSeconds,token);
                SetArtworkShape(card,data);
                return new HyTcgResult {Card=card,ImageData=data};
            }
            catch
            {
                token.ThrowIfCancellationRequested();
                return new HyTcgResult {Card=card,ImageData=Fallback(card,config,token)};
            }
        }
        internal HyTcgResult PrepareUpgrade(HyTcgUpgrade intent,HyTcgConfig config,CancellationToken token)
        {
            if(intent==null || intent.Card==null || String.IsNullOrWhiteSpace(intent.Token)) throw new HyTcgException("UpgradeInvalid");
            ValidateConfig(config);
            // Work on the native ledger's copy, never a renderer-provided character or rarity.
            var card=Parser().Deserialize<HyTcgCard>(Parser().Serialize(intent.Card));
            string data="";
            if(intent.Stage!="authorized" && intent.ChangeArtwork && !String.IsNullOrWhiteSpace(config.ZerochanUsername))
            {
                try
                {
                    string agent="HYTCG - "+config.ZerochanUsername;
                    var detail=FindArtwork(card,agent,config,token);
                    if(detail!=null)
                    {
                        int entryId=Number(detail,"id");
                        string image=First(detail,"full","medium","large","thumbnail");
                        if(!HyTcgTransport.AllowedImage(image)) throw new HyTcgException("ArtworkInvalid");
                        string candidateData=transport.Image(image,agent,config.RequestTimeoutSeconds,token);
                        var candidate=Parser().Deserialize<HyTcgCard>(Parser().Serialize(card));
                        SetArtworkShape(candidate,candidateData);
                        if(candidate.ArtworkWidth==0 && !candidateData.StartsWith("data:image/webp;base64,",StringComparison.Ordinal) && !candidateData.StartsWith("data:image/avif;base64,",StringComparison.Ordinal))
                            throw new HyTcgException("ArtworkInvalid");
                        candidate.ImageSource="zerochan";candidate.SourceImage=image;candidate.ArtworkPage="https://www.zerochan.net/"+entryId;
                        candidate.OriginalSource=First(detail,"source");candidate.FallbackReason="";
                        // Only adopt a decoded/bounded candidate. A bad optional image keeps the exact owned artwork.
                        card=candidate;data=candidateData;
                    }
                }
                catch(OperationCanceledException) {throw;}
                catch { /* An optional artwork change never destroys the current artwork. */ }
            }
            if(data=="")
            {
                token.ThrowIfCancellationRequested();
                // Preserve the exact source when keeping artwork, including when Zerochan is unavailable.
                data=transport.Image(card.SourceImage,card.ImageSource=="zerochan"?"HYTCG - "+config.ZerochanUsername:"HYTCG/1.0 (HYRO Launcher)",config.RequestTimeoutSeconds,token);
            }
            token.ThrowIfCancellationRequested();
            return new HyTcgResult {Card=card,ImageData=data};
        }
        internal static bool IsLandscape(HyTcgCard card)
        {return card!=null && String.Equals(card.Orientation,"landscape",StringComparison.Ordinal);}
        internal static int CanvasWidth(HyTcgCard card) {return IsLandscape(card)?1050:750;}
        internal static int CanvasHeight(HyTcgCard card) {return IsLandscape(card)?750:1050;}
        private static void SetArtworkShape(HyTcgCard card,string dataUrl)
        {
            int width=0,height=0;
            try
            {
                int comma=(dataUrl??"").IndexOf(',');
                if(comma<0 || dataUrl.Length>12*1024*1024)throw new HyTcgException("ArtworkInvalid");
                byte[] bytes=Convert.FromBase64String(dataUrl.Substring(comma+1));
                if(bytes.Length>8*1024*1024)throw new HyTcgException("ArtworkTooLarge");
                try
                {
                    using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream,false,false))
                    {width=image.Width;height=image.Height;}
                }
                catch
                {
                    // Windows GDI+ lacks WebP. Read its bounded RIFF dimension headers locally instead.
                    if(bytes.Length>=25 && Encoding.ASCII.GetString(bytes,0,4)=="RIFF" && Encoding.ASCII.GetString(bytes,8,4)=="WEBP")
                    {
                        string kind=Encoding.ASCII.GetString(bytes,12,4);
                        if(kind=="VP8X" && bytes.Length>=30)
                        {width=1+bytes[24]+(bytes[25]<<8)+(bytes[26]<<16);height=1+bytes[27]+(bytes[28]<<8)+(bytes[29]<<16);}
                        else if(kind=="VP8 " && bytes.Length>=30 && bytes[23]==157 && bytes[24]==1 && bytes[25]==42)
                        {width=(bytes[26]|bytes[27]<<8)&16383;height=(bytes[28]|bytes[29]<<8)&16383;}
                        else if(kind=="VP8L" && bytes.Length>=25 && bytes[20]==47)
                        {width=1+(bytes[21]|(bytes[22]&63)<<8);height=1+((bytes[22]>>6)|bytes[23]<<2|(bytes[24]&15)<<10);}
                    }
                    else if(bytes.Length>=32 && Encoding.ASCII.GetString(bytes,4,4)=="ftyp")
                    {
                        // AVIF's image spatial extents are an ISO BMFF ispe box (version/flags, width, height).
                        // Only bounded boxes in the downloaded bytes are accepted; no browser values enter the model.
                        for(int i=4;i+16<=bytes.Length;i++)
                        {
                            if(bytes[i]!=105 || bytes[i+1]!=115 || bytes[i+2]!=112 || bytes[i+3]!=101)continue;
                            uint boxSize=Png.Read32(bytes,i-4),w=Png.Read32(bytes,i+8),h=Png.Read32(bytes,i+12);
                            if(boxSize<20 || (long)(i-4)+boxSize>bytes.Length || w==0 || h==0)continue;
                            if(w>32768 || h>32768)throw new HyTcgException("ArtworkTooLarge");
                            width=(int)w;height=(int)h;break;
                        }
                    }
                }
                if(width<0 || height<0 || width>32768 || height>32768 || (long)width*height>50000000)throw new HyTcgException("ArtworkTooLarge");
            }
            catch(HyTcgException) {throw;}
            catch {width=0;height=0;} // Unknown browser-supported formats retain the portrait frame.
            card.ArtworkWidth=width;card.ArtworkHeight=height;
            card.Orientation="portrait";
        }
        private string ZerochanJson(string url,string agent,HyTcgConfig config,CancellationToken token)
        {
            WaitForZerochan(token);
            return transport.Json(url,agent,config.RequestTimeoutSeconds,token);
        }
        private List<Dictionary<string,object>> SearchArtworks(string url,string agent,HyTcgConfig config,CancellationToken token)
        {
            try {return ParseArtworks(ZerochanJson(url,agent,config,token));}
            catch(OperationCanceledException){throw;}
            catch(WebException error)
            {
                var response=error.Response as HttpWebResponse;int status=response==null?0:(int)response.StatusCode;if(response!=null)response.Dispose();
                if(status<500||status>599)throw;
                ApiActivityLog.Note("HYTCG","Zerochan JSON HTTP "+status+" → XML API");
            }
            catch(ArgumentException){ApiActivityLog.Note("HYTCG","Invalid Zerochan JSON → XML API");}
            catch(InvalidOperationException){ApiActivityLog.Note("HYTCG","Invalid Zerochan JSON → XML API");}
            return ParseArtworkXml(ZerochanJson(url.Replace("?json","?xml"),agent,config,token));
        }
        internal static List<Dictionary<string,object>> ParseArtworkXml(string xml)
        {
            var settings=new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024};
            using(var reader=XmlReader.Create(new StringReader(xml),settings))
            {
                var document=XDocument.Load(reader);XNamespace media="http://search.yahoo.com/mrss/";
                var result=new List<Dictionary<string,object>>();
                foreach(var item in document.Descendants("item").Take(250))
                {
                    Uri uri;int id;string link=(string)item.Element("link")??(string)item.Element("guid");
                    if(!Uri.TryCreate(link,UriKind.Absolute,out uri)||uri.Host!="www.zerochan.net"||!Int32.TryParse(uri.AbsolutePath.Trim('/'),out id)||id<1)continue;
                    string keywords=(string)item.Element(media+"keywords")??"";
                    var tags=keywords.Split(',').Select(tag=>Regex.Replace(tag.Trim(),@"\s+"," ")).ToArray();
                    var entry=new Dictionary<string,object>{{"id",id},{"tags",tags}};
                    if(SuitableArtwork(entry))result.Add(entry);
                }
                return result;
            }
        }
        private Dictionary<string,object> FindArtwork(HyTcgCard card,string agent,HyTcgConfig config,CancellationToken token)
        {
            string name=Uri.EscapeDataString(card.Name).Replace("%20","+"),series=(card.Series??"").Trim();
            // Multi-tag search is an intersection; strict is meaningful only for a single tag.
            string search="https://www.zerochan.net/"+name+(series==""?"":","+Uri.EscapeDataString(series).Replace("%20","+"))+"?json&l=50"+(series==""?"&strict":"");
            List<Dictionary<string,object>> entries;
            try {entries=SearchArtworks(search,agent,config,token);}
            catch(OperationCanceledException){throw;}
            catch {if(series=="")throw;entries=new List<Dictionary<string,object>>();}
            entries=entries.Where(entry=>!entry.ContainsKey("tags")||(ArtworkMatchesSeries(entry,series)&&ArtworkMatchesCharacter(entry,card.Name))).ToList();
            if(entries.Count==0 && series!="") entries=SearchArtworks("https://www.zerochan.net/"+name+"?json&strict&l=50",agent,config,token);
            ApiActivityLog.Note("HYTCG","Artwork candidates: "+entries.Count+" · "+card.Name+" / "+series);
            entries=entries.Where(entry=>!entry.ContainsKey("tags")||(ArtworkMatchesSeries(entry,series)&&ArtworkMatchesCharacter(entry,card.Name))).ToList();
            // Check a small random set of candidates. Never accept a name-only match from another series.
            for(int attempt=0;entries.Count>0 && attempt<4;attempt++)
            {
                token.ThrowIfCancellationRequested();int index=RandomIndex(entries.Count),id=Number(entries[index],"id");entries.RemoveAt(index);
                var detail=Parser().Deserialize<Dictionary<string,object>>(ZerochanJson("https://www.zerochan.net/"+id+"?json",agent,config,token));
                if(detail==null || !SuitableArtwork(detail) || !ArtworkMatchesSeries(detail,series)|| !ArtworkMatchesCharacter(detail,card.Name)){ApiActivityLog.Note("HYTCG","Rejected artwork #"+id+" · content/character/series mismatch · "+card.Name+" / "+series);continue;}
                detail["id"]=id;return detail;
            }
            return null;
        }
        internal static bool ArtworkMatchesSeries(Dictionary<string,object> detail,string series)
        {
            if(String.IsNullOrWhiteSpace(series))return true;
            var candidates=new List<string>();object raw;
            if(detail.TryGetValue("tags",out raw) && raw is IEnumerable && !(raw is string))
                foreach(object tag in (IEnumerable)raw)if(tag is string)candidates.Add((string)tag);
            foreach(string key in new[]{"anime","manga","game","series"}){string value=First(detail,key);if(value!="")candidates.Add(value);}
            string expected=SeriesKey(series);
            return expected!="" && candidates.Any(value=>SeriesKey(value)==expected);
        }
        internal static bool ArtworkMatchesCharacter(Dictionary<string,object> detail,string name)
        {
            string expected=CharacterKey(name);if(expected=="")return false;
            var candidates=new List<string>{First(detail,"primary")};object raw;
            if(detail.TryGetValue("tags",out raw)&&raw is IEnumerable&&!(raw is string))foreach(object tag in (IEnumerable)raw)if(tag is string)candidates.Add((string)tag);
            return candidates.Any(value=>CharacterKey(value)==expected);
        }
        private static string CharacterKey(string value)
        {
            string normalized=Regex.Replace((value??"").Normalize(NormalizationForm.FormKD).ToLowerInvariant(),@"\p{M}","");
            var words=Regex.Matches(normalized,@"[\p{L}\p{Nd}]+").Cast<Match>().Select(match=>match.Value).OrderBy(word=>word,StringComparer.Ordinal);
            return String.Join(" ",words.ToArray());
        }
        private static string SeriesKey(string value)
        {
            string key=Regex.Replace((value??"").Normalize(NormalizationForm.FormKD).ToLowerInvariant(),@"[^\p{L}\p{Nd}]","");
            switch(key)
            {
                case "puellamagimadokamagica":return "mahoushoujomadokamagica";
                case "myheroacademia":return "bokunoheroacademia";
                case "attackontitan":return "shingekinokyojin";
                case "demonslayerkimetsunoyaiba":case "demonslayer":return "kimetsunoyaiba";
                case "magiarecordpuellamagimadokamagicasidestory":case "puellamagimadokamagicasidestorymagiarecord":case "magiarecordmahoushoujomadokamagicagaiden":return "magiarecord";
                default:return key;
            }
        }
        internal static void WaitForZerochan(CancellationToken token)
        {
            int delay;
            lock(zerochanLock)
            {
                var now=DateTime.UtcNow;
                if(nextZerochan<now) nextZerochan=now;
                delay=(int)Math.Ceiling((nextZerochan-now).TotalMilliseconds);
                nextZerochan=nextZerochan.AddMilliseconds(1100); // Below the documented 60 API requests/minute.
            }
            Wait(delay,token);
        }
        private static void Wait(int milliseconds,CancellationToken token)
        {
            if(milliseconds>0 && token.WaitHandle.WaitOne(milliseconds)) token.ThrowIfCancellationRequested();
        }
        internal static HyTcgCard ParseCharacter(string json,int expectedId)
        {
            object parsed;
            try { parsed=Parser().DeserializeObject(json); } catch { return null; }
            var value=parsed as Dictionary<string,object>;
            if(value==null) return null;
            foreach(string key in new[]{"character","data"})
                if(value.ContainsKey(key) && value[key] is Dictionary<string,object>) { value=(Dictionary<string,object>)value[key]; break; }
            if(value.ContainsKey("characters") && value["characters"] is IEnumerable)
            {
                value=((IEnumerable)value["characters"]).Cast<object>().OfType<Dictionary<string,object>>().FirstOrDefault(item=>Number(item,"id","character_id")==expectedId);
            }
            if(value==null || Number(value,"id","character_id")!=expectedId) return null;
            string name=First(value,"name","character_name"),profile=NormalizeAcdbImage(First(value,"character_image","profile_image","image"));
            if(name=="" || profile=="" || name=="-1" || name.Length>180) return null;
            string series=First(value,"origin","anime_name","series","title");
            var metadata=new Dictionary<string,string>();
            foreach(string key in new[]{"desc","description","anime_id","eye_color","hair_color","hair_length","age","media_type"})
            { string text=First(value,key); if(text!="") metadata[key]=text; }
            return new HyTcgCard { CharacterId=expectedId,Name=name,Series=series,Gender=First(value,"gender"),ProfileImage=profile,Metadata=metadata };
        }
        internal static List<Dictionary<string,object>> ParseArtworks(string json)
        {
            var root=Parser().Deserialize<Dictionary<string,object>>(json);
            object items;
            if(root==null || !root.TryGetValue("items",out items) || !(items is IEnumerable)) return new List<Dictionary<string,object>>();
            return ((IEnumerable)items).Cast<object>().OfType<Dictionary<string,object>>().Where(item=>Number(item,"id")>0 && SuitableArtwork(item)).Take(250).ToList();
        }
        private static bool SuitableArtwork(Dictionary<string,object> item)
        {
            // Prefer ordinary character art over explicit/suggestive gallery results.
            object raw;
            if(!item.TryGetValue("tags",out raw) || !(raw is IEnumerable)) return true;
            var excluded=new HashSet<string>(new[]{"Ecchi","Nude","Nudity","Naked","Explicit","R-18","NSFW","Nipples","Exposed Breasts","Lingerie","Underwear","Fishnet Top","See-through Clothes","See-through Clothing","Transparent Clothes","Big Breasts","Cleavage"},StringComparer.OrdinalIgnoreCase);
            foreach(object tag in (IEnumerable)raw) if(excluded.Contains(Convert.ToString(tag))) return false;
            return true;
        }
        private static string NormalizeAcdbImage(string value)
        {
            if(String.IsNullOrWhiteSpace(value)) return "";
            Uri uri;
            if(!Uri.TryCreate(new Uri("https://www.animecharactersdatabase.com/"),WebUtility.HtmlDecode(value),out uri)) return "";
            if(uri.Scheme=="http") { var builder=new UriBuilder(uri); builder.Scheme="https"; builder.Port=-1; uri=builder.Uri; }
            return HyTcgTransport.AllowedImage(uri.AbsoluteUri) && uri.Host.EndsWith("animecharactersdatabase.com",StringComparison.Ordinal)?uri.AbsoluteUri:"";
        }
        private static string First(Dictionary<string,object> value,params string[] keys)
        {
            if(value==null) return "";
            foreach(string key in keys)
            {
                object raw;
                if(value.TryGetValue(key,out raw) && raw!=null && !(raw is IDictionary) && !(raw is object[]))
                { string text=WebUtility.HtmlDecode(Convert.ToString(raw,CultureInfo.InvariantCulture)).Trim(); if(text!="") return text.Length>1000?text.Substring(0,1000):text; }
            }
            return "";
        }
        private static int Number(Dictionary<string,object> value,params string[] keys)
        { int n; return Int32.TryParse(First(value,keys),out n)?n:0; }

        private void EnsureCardsDirectory()
        {
            Directory.CreateDirectory(CardsDirectory);
            if((File.GetAttributes(CardsDirectory)&FileAttributes.ReparsePoint)!=0) throw new HyTcgException("SaveFailed");
        }
        internal string SaveCard(HyTcgCard card,string dataUrl,string token)
        {
            using(Economy.BeginCardExport(token,card))
            {
            byte[] png=EncodeCard(card,dataUrl),bytes=HyTcgStorage.Protect(png);
            string fileName=CardFileStem(card)+".hycard";
            lock(storageLock)
            {
                EnsureCardsDirectory();
                string destination=Path.Combine(CardsDirectory,fileName),temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                if(File.Exists(destination))
                {
                    if((File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0 || new FileInfo(destination).Length>HyTcgStorage.MaxContainerBytes)throw new HyTcgException("CardTampered");
                    byte[] existing=File.ReadAllBytes(destination);
                    var stored=Economy.ReadCard(fileName,existing);
                    if(stored!=null && stored.CardId==card.CardId)return destination;
                    if(!HyTcgStorage.IsContainer(existing) || !HyTcgStorage.ImageBytes(existing).SequenceEqual(png))throw new HyTcgException("CardTampered");
                    Economy.Complete(token,card,fileName,existing);return destination;
                }
                // Keep an uncommitted encrypted candidate so the issued card can safely resume.
                // No visible image or wallet authority exists until Complete commits it.
                try { WriteCardTemp(temp,bytes); File.Move(temp,destination);Economy.Complete(token,card,fileName,bytes); }
                finally { if(File.Exists(temp)) File.Delete(temp); }
                return destination;
            }
            }
        }
        internal HyTcgUpgradeResult SaveUpgrade(HyTcgCard card,string dataUrl,string upgradeToken)
        {
            if(card==null || card.Revision<1 || !Regex.IsMatch(upgradeToken??"",@"^HU-[a-f0-9]{32}$"))throw new HyTcgException("UpgradeInvalid");
            using(Economy.BeginUpgradeExport(upgradeToken,card))
            {
            byte[] png=EncodeCard(card,dataUrl),bytes=HyTcgStorage.Protect(png);
            // A new version never overwrites the current card before the native ledger commits materials.
            string fileName=CardFileStem(card)+"_R"+card.Revision.ToString(CultureInfo.InvariantCulture)+"_"+upgradeToken.Substring(3)+".hycard";
            lock(storageLock)
            {
                EnsureCardsDirectory();
                string destination=Path.Combine(CardsDirectory,fileName),temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                if(File.Exists(destination))
                {
                    if((File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0 || new FileInfo(destination).Length>HyTcgStorage.MaxContainerBytes)throw new HyTcgException("CardTampered");
                    byte[] existing=File.ReadAllBytes(destination);
                    var stored=Economy.ReadCard(fileName,existing);
                    if(stored==null && (!HyTcgStorage.IsContainer(existing) || !HyTcgStorage.ImageBytes(existing).SequenceEqual(png)))throw new HyTcgException("CardTampered");
                    return Economy.CompleteUpgrade(upgradeToken,card,fileName,existing);
                }
                try
                {
                    WriteCardTemp(temp,bytes);File.Move(temp,destination);
                    return Economy.CompleteUpgrade(upgradeToken,card,fileName,bytes);
                }
                finally {if(File.Exists(temp))File.Delete(temp);}
            }
            }
        }
        private static void WriteCardTemp(string path,byte[] bytes)
        {
            // The owned image must reach disk before the durable wallet consumes an upgrade's materials.
            using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,8192,FileOptions.WriteThrough))
            {output.Write(bytes,0,bytes.Length);output.Flush(true);}
        }
        private static byte[] EncodeCard(HyTcgCard card,string dataUrl)
        {
            if(card==null || !Rarities.Contains(card.Rarity) || !Regex.IsMatch(card.CardId??"",@"^HT-\d{8}-[A-F0-9]{12}$")) throw new HyTcgException("CardInvalid");
            if(!String.IsNullOrEmpty(card.Orientation) && card.Orientation!="portrait" && card.Orientation!="landscape")throw new HyTcgException("CardInvalid");
            const string prefix="data:image/png;base64,";
            if(dataUrl==null || !dataUrl.StartsWith(prefix,StringComparison.Ordinal) || dataUrl.Length>10*1024*1024) throw new HyTcgException("CardInvalid");
            byte[] bytes;
            try
            {
                bytes=Convert.FromBase64String(dataUrl.Substring(prefix.Length));
                int width=CanvasWidth(card),height=CanvasHeight(card);
                if(bytes.Length<24 || Png.Read32(bytes,16)!=width || Png.Read32(bytes,20)!=height) throw new HyTcgException("CardInvalid");
                // Decode and re-encode: persist only a genuine rendered PNG, never renderer-supplied chunks.
                using(var input=new MemoryStream(bytes))
                using(var image=Image.FromStream(input,true,true))
                using(var output=new MemoryStream())
                {
                    if(image.RawFormat.Guid!=ImageFormat.Png.Guid || image.Width!=width || image.Height!=height) throw new HyTcgException("CardInvalid");
                    image.Save(output,ImageFormat.Png); bytes=output.ToArray();
                }
                bytes=Png.WithMetadata(bytes,Parser().Serialize(card));
            }
            catch(HyTcgException) { throw; }
            catch { throw new HyTcgException("CardInvalid"); }
            return bytes;
        }
        private static string CardFileStem(HyTcgCard card)
        {
            string safe=Regex.Replace((card.Name??"Character").Normalize(NormalizationForm.FormD),@"[^A-Za-z0-9]+","_").Trim('_');
            if(safe=="") safe="Character";
            if(safe.Length>48) safe=safe.Substring(0,48);
            return card.Rarity+"_"+safe+"_"+card.CardId.Substring(3);
        }
        internal HyTcgCollectionPage Collection(int offset,string rarity,string query)
        {return Collection(offset,rarity,query,false);}
        internal HyTcgCollectionPage Collection(int offset,string rarity,string query,bool favoritesOnly)
        {return Collection(offset,rarity,query,favoritesOnly,"newest");}
        internal HyTcgCollectionPage Collection(int offset,string rarity,string query,bool favoritesOnly,string sort)
        {
            EnsureCardsDirectory();
            return Economy.CollectionPage(offset,rarity,query,favoritesOnly,sort);
        }
        // Each PNG carries its own card metadata. There is no database or sidecar index.
        internal static class Png
        {
            private const string Keyword="HYTCG";
            internal static uint Read32(byte[] data,int position)
            { return ((uint)data[position]<<24)|((uint)data[position+1]<<16)|((uint)data[position+2]<<8)|data[position+3]; }
            private static void Write32(Stream stream,uint value)
            { stream.WriteByte((byte)(value>>24)); stream.WriteByte((byte)(value>>16)); stream.WriteByte((byte)(value>>8)); stream.WriteByte((byte)value); }
            private static uint Crc(byte[] data)
            {
                uint crc=0xffffffff;
                foreach(byte b in data) { crc^=b; for(int bit=0;bit<8;bit++) crc=(crc&1)!=0?0xedb88320^(crc>>1):crc>>1; }
                return crc^0xffffffff;
            }
            internal static byte[] WithMetadata(byte[] png,string json)
            {
                byte[] text=Encoding.ASCII.GetBytes(Keyword+"\0"+Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
                byte[] chunk=new byte[text.Length+4]; Encoding.ASCII.GetBytes("tEXt").CopyTo(chunk,0); text.CopyTo(chunk,4);
                using(var output=new MemoryStream())
                {
                    output.Write(png,0,33); Write32(output,(uint)text.Length); output.Write(chunk,0,chunk.Length); Write32(output,Crc(chunk));
                    output.Write(png,33,png.Length-33); return output.ToArray();
                }
            }
            internal static string Metadata(string path)
            {
                var file=new FileInfo(path);if(file.Length>HyTcgStorage.MaxContainerBytes || file.Length<24)return "";
                using(var stream=new MemoryStream(HyTcgStorage.ImageBytes(File.ReadAllBytes(path))))
                using(var reader=new BinaryReader(stream))
                {
                    byte[] signature=reader.ReadBytes(8);
                    if(signature.Length!=8 || signature[0]!=137 || signature[1]!=80 || signature[2]!=78 || signature[3]!=71) return "";
                    for(int i=0;i<100 && stream.Position+12<=stream.Length;i++)
                    {
                        byte[] header=reader.ReadBytes(8); uint size=Read32(header,0); string type=Encoding.ASCII.GetString(header,4,4);
                        if(size>32*1024*1024 || stream.Position+size+4>stream.Length) return "";
                        if(type=="tEXt" && size<=32768)
                        {
                            byte[] data=reader.ReadBytes((int)size),crc=reader.ReadBytes(4),chunk=new byte[data.Length+4];
                            Encoding.ASCII.GetBytes(type).CopyTo(chunk,0); data.CopyTo(chunk,4);
                            string text=Encoding.ASCII.GetString(data);
                            if(text.StartsWith(Keyword+"\0",StringComparison.Ordinal) && Read32(crc,0)==Crc(chunk))
                                return Encoding.UTF8.GetString(Convert.FromBase64String(text.Substring(Keyword.Length+1)));
                        }
                        else { stream.Seek(size+4,SeekOrigin.Current); }
                        if(type=="IEND") break;
                    }
                }
                return "";
            }
        }
    }
}
