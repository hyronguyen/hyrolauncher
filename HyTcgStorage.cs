using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HYROLauncherWeb
{
    // Images remain local, but their on-disk container cannot be previewed as an image.
    // DPAPI binds decryption to this Windows account; the vault signs the entire container.
    internal static class HyTcgStorage
    {
        private static readonly byte[] magic=Encoding.ASCII.GetBytes("HYTCG-CARD2\0");
        private static readonly byte[] entropy=Encoding.UTF8.GetBytes("HYROLauncher.HYTCG.card.v2");
        internal const int MaxContainerBytes=16*1024*1024+65536;
        internal static bool IsContainer(byte[] bytes)
        {
            if(bytes==null || bytes.Length<=magic.Length) return false;
            for(int i=0;i<magic.Length;i++) if(bytes[i]!=magic[i]) return false;
            return true;
        }
        internal static byte[] Protect(byte[] png)
        {
            try
            {
                byte[] protectedBytes=ProtectedData.Protect(png,entropy,DataProtectionScope.CurrentUser);
                byte[] result=new byte[magic.Length+protectedBytes.Length];
                Buffer.BlockCopy(magic,0,result,0,magic.Length);
                Buffer.BlockCopy(protectedBytes,0,result,magic.Length,protectedBytes.Length);
                if(result.Length>MaxContainerBytes) throw new HyTcgException("CardInvalid");
                return result;
            }
            catch(HyTcgException) { throw; }
            catch { throw new HyTcgException("SaveFailed"); }
        }
        internal static byte[] ImageBytes(byte[] bytes)
        {
            if(bytes==null || bytes.Length>MaxContainerBytes) throw new HyTcgException("CardInvalid");
            if(!IsContainer(bytes)) return bytes;
            try
            {
                byte[] protectedBytes=new byte[bytes.Length-magic.Length];
                Buffer.BlockCopy(bytes,magic.Length,protectedBytes,0,protectedBytes.Length);
                byte[] png=ProtectedData.Unprotect(protectedBytes,entropy,DataProtectionScope.CurrentUser);
                if(png.Length>16*1024*1024) throw new HyTcgException("CardInvalid");
                return png;
            }
            catch { throw new HyTcgException("CardInvalid"); }
        }
        internal static string Thumbnail(byte[] bytes)
        {
            byte[] png=ImageBytes(bytes);
            using(var input=new MemoryStream(png))
            using(var image=Image.FromStream(input,true,true))
            {
                double ratio=Math.Min(392d/image.Width,392d/image.Height);
                int width=Math.Max(1,(int)Math.Round(image.Width*ratio)),height=Math.Max(1,(int)Math.Round(image.Height*ratio));
                using(var thumbnail=new Bitmap(width,height))
                using(var graphics=Graphics.FromImage(thumbnail))
                using(var output=new MemoryStream())
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingQuality=CompositingQuality.HighQuality;
                    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    graphics.DrawImage(image,new Rectangle(0,0,width,height));
                    thumbnail.Save(output,ImageFormat.Png);
                    return "data:image/png;base64,"+Convert.ToBase64String(output.ToArray());
                }
            }
        }
    }

    internal sealed partial class HyTcgEconomy
    {
        private sealed class CachedImage
        {
            internal string Image;
            internal LinkedListNode<string> Node;
        }
        private sealed class ImageMemoryCache
        {
            private readonly int maximumCount,maximumBytes;
            private int bytes;
            private readonly Dictionary<string,CachedImage> values=new Dictionary<string,CachedImage>(StringComparer.Ordinal);
            private readonly LinkedList<string> order=new LinkedList<string>();
            internal ImageMemoryCache(int count,int size) {maximumCount=count;maximumBytes=size;}
            internal bool TryGet(string key,out string image)
            {
                CachedImage item;
                if(!values.TryGetValue(key,out item)) {image=null;return false;}
                order.Remove(item.Node);order.AddLast(item.Node);image=item.Image;return true;
            }
            internal void Add(string key,string image)
            {
                if(image==null || image.Length>maximumBytes/2)return;
                CachedImage previous;
                if(values.TryGetValue(key,out previous)) {bytes-=previous.Image.Length*2;order.Remove(previous.Node);values.Remove(key);}
                var item=new CachedImage {Image=image,Node=order.AddLast(key)};values[key]=item;bytes+=image.Length*2;
                while(values.Count>maximumCount || bytes>maximumBytes)
                {
                    string first=order.First.Value;bytes-=values[first].Image.Length*2;values.Remove(first);order.RemoveFirst();
                }
            }
            internal void Clear() {values.Clear();order.Clear();bytes=0;}
        }
        // All cache access runs inside the native wallet gate. Cached pictures never
        // replace verification of the current signed ciphertext on disk.
        private readonly ImageMemoryCache imagePreviewCache=new ImageMemoryCache(200,8*1024*1024);
        private readonly ImageMemoryCache imageDetailCache=new ImageMemoryCache(8,16*1024*1024);
        private long imageReads,imageDecryptions,previewGenerations,previewMemoryHits,previewDiskHits,detailMemoryHits;
        private bool imageCacheTrimmed;
        private static readonly byte[] previewMagic=Encoding.ASCII.GetBytes("HYTCG-PREVIEW2\0");
        private static readonly byte[] previewEntropy=Encoding.UTF8.GetBytes("HYROLauncher.HYTCG.preview.v2.alpha");

        // Export activity belongs to the native wallet, so a refreshed/closed UI cannot
        // hide a disk write from account reset. No wallet lock is held across rendering.
        private sealed class ExportLease : IDisposable
        {
            private HyTcgEconomy owner;
            private readonly string key;
            internal ExportLease(HyTcgEconomy owner,string key) {this.owner=owner;this.key=key;}
            public void Dispose()
            {
                HyTcgEconomy target=System.Threading.Interlocked.Exchange(ref owner,null);
                if(target!=null)lock(target.gate)target.active.Remove(key);
            }
        }
        internal IDisposable BeginCardExport(string token,HyTcgCard card)
        {
            ValidateCard(card);
            if(!System.Text.RegularExpressions.Regex.IsMatch(token??"",@"^HC-[a-f0-9]{32}$"))throw new HyTcgException("CardInvalid");
            string key="export:"+token;
            lock(gate)
            {
                if(active.Contains(key))throw new HyTcgException("Busy");
                Read(delegate(Vault v,DateTime now)
                {
                    var owned=v.Cards.FirstOrDefault(c=>c.CardId==card.CardId);
                    if(owned!=null)
                    {
                        if(owned.Legacy || owned.Dissolved || owned.Fingerprint!=Fingerprint(card) || !Verify(v,owned,owned.FileName,ReadPng(owned.FileName)))throw new HyTcgException("CardInvalid");
                    }
                    else
                    {
                        Pending pending=FindPending(v,token);
                        if(pending.Stage!="authorized" || pending.Card==null || pending.Card.CardId!=card.CardId || pending.Fingerprint!=Fingerprint(card))throw new HyTcgException("CardInvalid");
                    }
                    return true;
                });
                active.Add(key);return new ExportLease(this,key);
            }
        }
        internal IDisposable BeginUpgradeExport(string token,HyTcgCard card)
        {
            ValidateCard(card);
            if(!System.Text.RegularExpressions.Regex.IsMatch(token??"",@"^HU-[a-f0-9]{32}$"))throw new HyTcgException("UpgradeInvalid");
            string key="export:"+token;
            lock(gate)
            {
                if(active.Contains(key))throw new HyTcgException("Busy");
                Read(delegate(Vault v,DateTime now)
                {
                    UpgradeIntent intent=FindUpgrade(v,token);
                    if((intent.Stage!="authorized" && intent.Stage!="complete") || intent.Fingerprint!=Fingerprint(card))throw new HyTcgException("UpgradeInvalid");
                    if(intent.Stage=="authorized")ValidateUpgradeParticipants(v,intent);
                    else
                    {
                        Owned owned=VerifiedOwned(v,intent.TargetId,false);
                        if(owned.Fingerprint!=intent.Fingerprint || owned.FileName!=intent.CompletedFileName)throw new HyTcgException("UpgradeInvalid");
                    }
                    return true;
                });
                active.Add(key);return new ExportLease(this,key);
            }
        }

        // Commit filenames and ciphertext hashes together. A crash before the vault commit
        // leaves the original usable; a crash after it leaves only a harmless cleanup task.
        internal int MigrateEncryptedStorage()
        {
            bool required=Read(delegate(Vault v,DateTime now)
            {
                foreach(Owned owned in v.Cards)
                {
                    if(owned.FileName.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return true;
                    if(owned.FileName.EndsWith(".hycard",StringComparison.OrdinalIgnoreCase))
                        try {if(File.Exists(SafeCardPath(Path.ChangeExtension(owned.FileName,".png"))))return true;}catch(HyTcgException) { }
                }
                return false;
            });
            if(!required)return 0;
            int converted=0;
            var cleanup=Write(delegate(Vault v,DateTime now)
            {
                var names=new List<string>();
                foreach(Owned owned in v.Cards.ToList())
                {
                    if(owned.FileName.EndsWith(".hycard",StringComparison.OrdinalIgnoreCase)) { names.Add(owned.FileName); continue; }
                    if(!owned.FileName.EndsWith(".png",StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        byte[] original=ReadPng(owned.FileName);
                        if(!Verify(v,owned,owned.FileName,original)) continue;
                        string nextName=Path.ChangeExtension(owned.FileName,".hycard"),destination=SafeCardPath(nextName);
                        if(v.Cards.Any(other=>!Object.ReferenceEquals(other,owned) && String.Equals(other.FileName,nextName,StringComparison.OrdinalIgnoreCase))) continue;
                        byte[] container;
                        if(File.Exists(destination))
                        {
                            container=ReadPng(nextName);
                            if(!HyTcgStorage.IsContainer(container) || !ImageEquals(original,HyTcgStorage.ImageBytes(container))) continue;
                        }
                        else
                        {
                            container=HyTcgStorage.Protect(original);
                            string temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                            try { WriteImageTemp(temp,container); File.Move(temp,destination); }
                            finally { if(File.Exists(temp)) File.Delete(temp); }
                        }
                        var replacement=CreateOwned(v,owned.Card,nextName,container,owned.Legacy);
                        replacement.Favorite=owned.Favorite;replacement.Dissolved=owned.Dissolved;
                        v.Cards[v.Cards.IndexOf(owned)]=replacement;
                        foreach(UpgradeIntent intent in v.Upgrades)
                        {
                            if(String.Equals(intent.OriginalFileName,owned.FileName,StringComparison.OrdinalIgnoreCase)) intent.OriginalFileName=nextName;
                            if(String.Equals(intent.CompletedFileName,owned.FileName,StringComparison.OrdinalIgnoreCase)) intent.CompletedFileName=nextName;
                        }
                        names.Add(nextName);converted++;
                    }
                    catch(IOException) { }
                    catch(UnauthorizedAccessException) { }
                    catch(HyTcgException error) { if(error.Code=="VaultInvalid" || error.Code=="VaultUnavailable") throw; }
                }
                return names;
            });
            // Cleanup always re-authenticates the committed destination and compares the old
            // plaintext bytes. It never removes an unowned, changed or linked file.
            Read(delegate(Vault v,DateTime now)
            {
                foreach(string name in cleanup)
                {
                    try
                    {
                        Owned owned=v.Cards.FirstOrDefault(c=>String.Equals(c.FileName,name,StringComparison.OrdinalIgnoreCase));
                        if(owned==null) continue;
                        string previous=SafeCardPath(Path.ChangeExtension(name,".png"));
                        if(!File.Exists(previous)) continue;
                        byte[] container=ReadPng(name);
                        if(!Verify(v,owned,name,container)) continue;
                        var file=new FileInfo(previous);if(file.Length>MaxPngBytes || file.Length<24) continue;
                        if(ImageEquals(File.ReadAllBytes(previous),HyTcgStorage.ImageBytes(container))) File.Delete(previous);
                    }
                    catch(IOException) { }
                    catch(UnauthorizedAccessException) { }
                    catch(HyTcgException) { }
                }
                return true;
            });
            return converted;
        }
        private static bool ImageEquals(byte[] a,byte[] b) { return EqualHash(Hash(a),Hash(b)); }
        private static void WriteImageTemp(string path,byte[] bytes)
        {
            using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,8192,FileOptions.WriteThrough))
            { output.Write(bytes,0,bytes.Length);output.Flush(true); }
        }
        internal string ReadCardImage(string cardId)
        {
            return Read(delegate(Vault v,DateTime now)
            {
                Owned owned=ImageOwner(v,cardId);byte[] container=ReadOwnedContainer(v,owned);string image;
                if(imageDetailCache.TryGet(owned.PngHash,out image)) {detailMemoryHits++;return image;}
                image="data:image/png;base64,"+Convert.ToBase64String(DecryptOwnedImage(owned,container));
                imageDetailCache.Add(owned.PngHash,image);return image;
            });
        }
        // Readonly enrichment needs the native-issued identity, not its artwork.
        // Economic actions and image display continue to verify the full file.
        internal HyTcgCard ReadOwnedCardMetadata(string cardId)
        {return Read(delegate(Vault v,DateTime now) {return Copy(ImageOwner(v,cardId).Card);});}
        internal HyTcgCollectionPage CollectionPage(int offset,string rarity,string query,bool favoritesOnly,string sort)
        {
            return Read(delegate(Vault v,DateTime now)
            {
                // Filter the protected metadata before touching artwork. Scrolling a
                // collection reads only this page, rather than decoding every card.
                var owned=CollectionIndex(v);
                var cards=sort=="rarity" ? owned.OrderByDescending(item=>Array.IndexOf(HyTcgService.Rarities,item.Card.Rarity)).ThenByDescending(item=>item.Card.CreatedAt).ThenBy(item=>item.FileName)
                    : sort=="name" ? owned.OrderBy(item=>item.Card.Name,StringComparer.OrdinalIgnoreCase).ThenBy(item=>item.FileName)
                    : owned.OrderByDescending(item=>item.Card.CreatedAt).ThenBy(item=>item.FileName);
                string search=(query??"").Trim();
                var filtered=cards.Where(item=>(!favoritesOnly || item.Favorite) && (String.IsNullOrEmpty(rarity) || item.Card.Rarity==rarity)
                    && (search=="" || (item.Card.Name+" "+item.Card.Series).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
                var page=new HyTcgCollectionPage {Offset=Math.Max(0,offset),Total=filtered.Count,Items=filtered.Skip(Math.Max(0,offset)).Take(40).ToList()};
                int requested=page.Items.Count;page.NextOffset=page.Offset+requested;PopulateImagePreviews(v,page.Items);
                page.Total-=requested-page.Items.Count;
                // Pagination tracks metadata slots, so a broken file cannot make the
                // following request repeat a valid card from this page.
                page.HasMore=page.Offset+requested<filtered.Count;return page;
            });
        }
        private List<HyTcgCollectionItem> CollectionIndex(Vault v)
        {
            var result=new List<HyTcgCollectionItem>();
            var locked=new HashSet<string>(v.Upgrades.Where(u=>u.Stage!="complete").SelectMany(u=>u.MaterialIds.Concat(new[]{u.TargetId})),StringComparer.Ordinal);
            if(v.Gameplay!=null)locked.UnionWith(v.Gameplay.Expeditions.Where(e=>!e.Claimed).SelectMany(e=>e.CardIds));
            foreach(Owned owned in v.Cards.Where(c=>!c.Dissolved))
            {
                try
                {
                    if(!ImageMetadataValid(v,owned))continue;
                    string path=SafeCardPath(owned.FileName);var file=new FileInfo(path);
                    if(!file.Exists || file.Length<24 || file.Length>HyTcgStorage.MaxContainerBytes)continue;
                    bool unavailable=locked.Contains(owned.CardId);
                    result.Add(new HyTcgCollectionItem {Card=Copy(owned.Card),FileName=owned.FileName,ImagePath=path,
                        CanDissolve=!owned.Legacy && !unavailable,SalvageValue=owned.Legacy?0:HyTcgRules.SalvageValue(owned.Card.Rarity),IsLegacy=owned.Legacy,
                        Favorite=owned.Favorite,UpgradeLocked=unavailable,ExpeditionLocked=IsGameplayLocked(v,owned.CardId),CanUpgrade=!owned.Legacy && !unavailable && HyTcgRules.RarityIndex(owned.Card.Rarity)<5});
                }
                catch(IOException) { }
                catch(UnauthorizedAccessException) { }
                catch(HyTcgException) { }
            }
            return result;
        }
        private static bool ImageMetadataValid(Vault v,Owned owned)
        {return owned!=null && !owned.Dissolved && owned.Fingerprint==Fingerprint(owned.Card) && EqualHash(owned.Signature,Sign(v,owned));}
        private static Owned ImageOwner(Vault v,string cardId)
        {
            Owned owned=v.Cards.FirstOrDefault(c=>c.CardId==cardId);
            if(owned==null || owned.Dissolved)throw new HyTcgException("CardUnavailable");
            if(!ImageMetadataValid(v,owned))throw new HyTcgException("CardInvalid");return owned;
        }
        private byte[] ReadOwnedContainer(Vault v,Owned owned)
        {
            if(!ImageMetadataValid(v,owned))throw new HyTcgException("CardInvalid");
            string path=SafeCardPath(owned.FileName);var file=new FileInfo(path);
            if(!file.Exists)throw new HyTcgException("CardUnavailable");
            if(file.Length<24 || file.Length>HyTcgStorage.MaxContainerBytes)throw new HyTcgException("CardInvalid");
            byte[] bytes=File.ReadAllBytes(path);imageReads++;
            if(bytes.Length>HyTcgStorage.MaxContainerBytes || (owned.FileName.EndsWith(".hycard",StringComparison.OrdinalIgnoreCase) && !HyTcgStorage.IsContainer(bytes))
                || !EqualHash(owned.PngHash,Hash(bytes)))throw new HyTcgException("CardInvalid");
            return bytes;
        }
        private byte[] DecryptOwnedImage(Owned owned,byte[] container)
        {
            byte[] png=HyTcgStorage.ImageBytes(container);imageDecryptions++;ValidateCardBytes(owned.Card,png);return png;
        }
        internal void PopulateImagePreviews(List<HyTcgCollectionItem> items)
        {
            Read(delegate(Vault v,DateTime now)
            {
                PopulateImagePreviews(v,items);
                return true;
            });
        }
        private void PopulateImagePreviews(Vault v,List<HyTcgCollectionItem> items)
        {
            foreach(var item in items)
            {
                try
                {
                    Owned owned=ImageOwner(v,item.Card.CardId);item.SalvageValue=owned.Legacy?0:SalvageValue(owned.Card);
                    byte[] container=ReadOwnedContainer(v,owned);string preview;
                    if(imagePreviewCache.TryGet(owned.PngHash,out preview))previewMemoryHits++;
                    else
                    {
                        preview=ReadPreview(v,owned.PngHash);
                        if(preview==null)
                        {
                            preview=HyTcgStorage.Thumbnail(DecryptOwnedImage(owned,container));previewGenerations++;
                            SavePreview(v,owned.PngHash,preview);
                        }
                        else previewDiskHits++;
                        imagePreviewCache.Add(owned.PngHash,preview);
                    }
                    item.ImagePath=preview;
                }
                catch(IOException) {item.ImagePath=null;}
                catch(UnauthorizedAccessException) {item.ImagePath=null;}
                catch(ArgumentException) {item.ImagePath=null;}
                catch(HyTcgException error)
                {if(error.Code=="VaultInvalid" || error.Code=="VaultUnavailable")throw;item.ImagePath=null;}
            }
            items.RemoveAll(item=>String.IsNullOrEmpty(item.ImagePath));
        }
        private string PreviewDirectory()
        {
            string directory=Path.Combine(root,"hytcg-previews");Directory.CreateDirectory(directory);
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked preview directory");
            return directory;
        }
        private string PreviewPath(string hash)
        {
            if(!ValidHash(hash))throw new IOException("Invalid preview key");
            string path=Path.Combine(PreviewDirectory(),hash+".hypreview");
            if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked preview file");return path;
        }
        private static byte[] PreviewSignature(Vault v,string hash,byte[] ciphertext)
        {
            using(var hmac=new HMACSHA256(Convert.FromBase64String(v.Key)))
            {
                byte[] key=Encoding.ASCII.GetBytes(hash);hmac.TransformBlock(key,0,key.Length,null,0);
                hmac.TransformFinalBlock(ciphertext,0,ciphertext.Length);return hmac.Hash;
            }
        }
        private string ReadPreview(Vault v,string hash)
        {
            try
            {
                string path=PreviewPath(hash);var file=new FileInfo(path);if(!file.Exists || file.Length<previewMagic.Length+64 || file.Length>512*1024)return null;
                byte[] bytes=File.ReadAllBytes(path);if(bytes.Length>512*1024)return null;
                for(int i=0;i<previewMagic.Length;i++)if(bytes[i]!=previewMagic[i])return null;
                int start=previewMagic.Length+32;byte[] ciphertext=new byte[bytes.Length-start];Buffer.BlockCopy(bytes,start,ciphertext,0,ciphertext.Length);
                byte[] signature=PreviewSignature(v,hash,ciphertext);int different=0;for(int i=0;i<32;i++)different|=signature[i]^bytes[previewMagic.Length+i];if(different!=0)return null;
                byte[] png=ProtectedData.Unprotect(ciphertext,previewEntropy,DataProtectionScope.CurrentUser);
                if(png.Length<24 || png.Length>384*1024 || png[0]!=137 || png[1]!=80 || png[2]!=78 || png[3]!=71)return null;
                return "data:image/png;base64,"+Convert.ToBase64String(png);
            }
            catch {return null;}
        }
        private void SavePreview(Vault v,string hash,string preview)
        {
            string temporary=null;
            try
            {
                string path=PreviewPath(hash);byte[] jpeg=Convert.FromBase64String(preview.Substring(preview.IndexOf(',')+1));
                if(jpeg.Length>384*1024)return;
                byte[] ciphertext=ProtectedData.Protect(jpeg,previewEntropy,DataProtectionScope.CurrentUser),signature=PreviewSignature(v,hash,ciphertext);
                byte[] bytes=new byte[previewMagic.Length+signature.Length+ciphertext.Length];
                Buffer.BlockCopy(previewMagic,0,bytes,0,previewMagic.Length);Buffer.BlockCopy(signature,0,bytes,previewMagic.Length,signature.Length);
                Buffer.BlockCopy(ciphertext,0,bytes,previewMagic.Length+signature.Length,ciphertext.Length);
                if(File.Exists(path))File.Delete(path);
                temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllBytes(temporary,bytes);File.Move(temporary,path);temporary=null;
                TrimPreviewCache();
            }
            catch { }
            finally {if(temporary!=null)try {File.Delete(temporary);}catch { }}
        }
        private void TrimPreviewCache()
        {
            // First use also removes abandoned temporary cache writes after a crash.
            string directory=PreviewDirectory();var files=new DirectoryInfo(directory).GetFiles().Where(f=>(f.Attributes&FileAttributes.ReparsePoint)==0).ToList();
            if(!imageCacheTrimmed)
            {
                foreach(var file in files.Where(f=>f.Name.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)))try {file.Delete();}catch { }
                imageCacheTrimmed=true;
            }
            files=files.Where(f=>f.Extension==".hypreview").OrderByDescending(f=>f.LastWriteTimeUtc).ToList();
            long total=0;for(int i=0;i<files.Count;i++) {total+=files[i].Length;if(i>=256 || total>32*1024*1024)try {files[i].Delete();}catch { }}
        }
        internal void ClearImageCaches()
        {
            lock(gate)
            {
                imagePreviewCache.Clear();imageDetailCache.Clear();
                HyTcgCosmeticRenderer.ClearCache();
                try
                {
                    string directory=PreviewDirectory();foreach(string path in Directory.GetFiles(directory))
                        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0 && (path.EndsWith(".hypreview",StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)))try {File.Delete(path);}catch { }
                }
                catch { }
            }
        }
    }

    internal sealed partial class HyTcgService
    {
        private bool storageMigrated;
        internal string ReadCardImage(string cardId) { return Economy.ReadCardImage(cardId); }
        internal void PrepareCollectionImages(List<HyTcgCollectionItem> items) { Economy.PopulateImagePreviews(items); }
    }
}
