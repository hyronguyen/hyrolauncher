using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal static class VerifyHyTcgCreative
{
    private static int checks;
    private static readonly JavaScriptSerializer parser=new JavaScriptSerializer {MaxJsonLength=32*1024*1024};
    private static void Check(bool ok,string message) {checks++;if(!ok)throw new Exception(message);}
    private static void Error(string code,Action action)
    {try {action();throw new Exception("Expected "+code);}catch(HyTcgException ex) {Check(ex.Code==code,code+" got "+ex.Code);}}
    private static Dictionary<string,object> Input(params object[] pairs)
    {var result=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)result[(string)pairs[i]]=pairs[i+1];return result;}
    private static Dictionary<string,object> Snapshot(HyTcgEconomy economy)
    {return parser.Deserialize<Dictionary<string,object>>(parser.Serialize(economy.CreativeSnapshot()));}
    private static object Studio(HyTcgEconomy economy,Dictionary<string,object> input)
    {input["operationId"]="SC-"+Snapshot(economy)["NextStudioSequence"]+"-"+Guid.NewGuid().ToString("N");return economy.CreativeAction("cosmeticSave",input);}
    private static void FundAlbum(HyTcgEconomy economy)
    {if(!economy.VaultPath.Contains(Path.Combine("output","verification")))throw new Exception("Isolated fixture required");var p=new HyTcgUserProtector();var v=parser.Deserialize<Dictionary<string,object>>(System.Text.Encoding.UTF8.GetString(p.Unprotect(File.ReadAllBytes(economy.VaultPath))));v["Gems"]=Convert.ToInt32(v["Gems"])+2000;File.WriteAllBytes(economy.VaultPath,p.Protect(System.Text.Encoding.UTF8.GetBytes(parser.Serialize(v))));}
    private static object CreateAlbum(HyTcgEconomy economy,string name,string background)
    {FundAlbum(economy);var input=Input("name",name,"background",background,"cover",background,"operationId","SC-"+Snapshot(economy)["NextStudioSequence"]+"-"+Guid.NewGuid().ToString("N"));return economy.CreativeAction("albumCreate",input);}
    private static void LegacyStyle(HyTcgEconomy economy,HyTcgCosmetic style)
    {
        var protector=new HyTcgUserProtector();var vault=parser.Deserialize<Dictionary<string,object>>(System.Text.Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(economy.VaultPath))));
        var state=parser.Deserialize<HyTcgCreativeState>(parser.Serialize(vault["Creative"]));
        state.StudioVersion=0;state.Dyes=null;state.OwnedFrameIds=null;state.StudioReceipts=null;state.LastStudioReceipt=null;state.ResetCosmetics=null;state.FrameItems=null;state.StudioSequence=0;
        state.Cosmetics.Add(style);vault["Creative"]=state;
        File.WriteAllBytes(economy.VaultPath,protector.Protect(System.Text.Encoding.UTF8.GetBytes(parser.Serialize(vault))));
    }
    private static Dictionary<string,object>[] Items(Dictionary<string,object> snapshot,string name)
    {return Values(snapshot[name]).Cast<Dictionary<string,object>>().ToArray();}
    private static object[] Values(object array) {return ((IEnumerable)array).Cast<object>().ToArray();}
    private static byte[] Picture(int width,int height,Color color,ImageFormat format)
    {
        using(var bitmap=new Bitmap(width,height))using(var graphics=Graphics.FromImage(bitmap))using(var output=new MemoryStream())
        {graphics.Clear(color);bitmap.Save(output,format);return output.ToArray();}
    }
    private static HyTcgCard AddCard(HyTcgEconomy economy,string root,int serial,string rarity)
    {
        var card=new HyTcgCard {CharacterId=555,Name="Creative fixture",Series="Offline fixture",Gender="Female",Rarity=rarity,
            RarityColor=HyTcgRules.RarityColor(rarity),CreatedAt="2026-10-10T00:00:00Z",CardId="HT-20261010-"+serial.ToString("X12"),ImageSource="acdb"};
        var reservation=economy.Reserve(1);economy.Begin(reservation.Tokens[0]);economy.AuthorizeCard(reservation.Tokens[0],card);
        byte[] png=Picture(750,1050,Color.DarkSlateBlue,ImageFormat.Png),encrypted=HyTcgStorage.Protect(png);
        string file=card.CardId+".hycard";Directory.CreateDirectory(Path.Combine(root,"cards"));File.WriteAllBytes(Path.Combine(root,"cards",file),encrypted);
        economy.Complete(reservation.Tokens[0],card,file,encrypted);return card;
    }
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);
            if(root.IndexOf("output"+Path.DirectorySeparatorChar+"verification",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Creative fixture must stay in output/verification.");
            DateTime now=new DateTime(2026,10,10,1,0,0,DateTimeKind.Utc);
            var economy=new HyTcgEconomy(root,()=>now,new HyTcgUserProtector(),()=>TimeSpan.Zero);
            var initial=Snapshot(economy);Check(Items(initial,"Albums").Length==0 && Items(initial,"Cosmetics").Length==0,"fresh creative snapshot");
            Check(Items(initial,"Frames").Length==5 && Items(initial,"Backgrounds").Length==6,"native preset inventory");
            HyTcgCard target=AddCard(economy,root,1,"Common"),material=AddCard(economy,root,2,"Uncommon");
            string identity=parser.Serialize(economy.ReadOwnedCard(target.CardId));int gems=economy.Snapshot().Gems;
            byte[] beforeLookup=File.ReadAllBytes(economy.VaultPath);
            Check(economy.GetCardCosmetic(target.CardId)==null && File.ReadAllBytes(economy.VaultPath).SequenceEqual(beforeLookup),"default native style lookup leaves vault and creative state untouched");
            Error("CardUnavailable",()=>economy.GetCardCosmetic("HT-20261010-FFFFFFFFFFFF"));
            CreateAlbum(economy,"  My album  ","sakura");string purchasedId=(string)Items(Snapshot(economy),"Albums").Single()["Id"];
            economy.CreativeAction("albumSave",Input("albumId",purchasedId,"name","My album","cardIds",new object[]{target.CardId,"",null,material.CardId}));
            var album=Items(Snapshot(economy),"Albums").Single();string albumId=(string)album["Id"];
            Check((string)album["Name"]=="My album" && Values(album["CardIds"]).Length==4 && (string)Values(album["CardIds"])[2]=="","album trims name and preserves empty slots");
            string date=(string)album["CreatedAt"];
            economy.CreativeAction("albumSave",Input("albumId",albumId,"name","Ordered","cardIds",new object[]{material.CardId,target.CardId}));
            album=Items(Snapshot(economy),"Albums").Single();Check((string)album["CreatedAt"]==date && (string)Values(album["CardIds"])[0]==material.CardId,"updates preserve album ID, date and slot order");
            Error("AlbumInvalid",()=>economy.CreativeAction("albumSave",Input("name","","cardIds",new string[0])));
            Error("AlbumRestyleRequired",()=>economy.CreativeAction("albumSave",Input("albumId",albumId,"name","Bad","background","url(http://example.com)","cardIds",new string[0])));
            Error("AlbumInvalid",()=>economy.CreativeAction("albumSave",Input("name","Bad","cardIds",Enumerable.Repeat("",121).ToArray())));
            Error("AlbumInvalid",()=>economy.CreativeAction("albumSave",Input("name","Bad","cardIds",new[]{target.CardId,target.CardId})));
            Error("CardUnavailable",()=>economy.CreativeAction("albumSave",Input("name","Bad","cardIds",new[]{"HT-20261010-FFFFFFFFFFFF"})));
            Error("AlbumUnavailable",()=>economy.CreativeAction("albumDelete",Input("albumId","AL-"+new string('0',32))));
            byte[] original=Picture(2200,1200,Color.FromArgb(100,200,40,80),ImageFormat.Png);
            string artId=economy.StoreCustomArtwork(original),imageData=economy.ReadCustomArtwork(artId);
            byte[] normalized=Convert.FromBase64String(imageData.Substring(imageData.IndexOf(',')+1));
            using(var stream=new MemoryStream(normalized))using(var image=Image.FromStream(stream))Check(image.Width==1600 && image.Height==873,"normalization bounds dimensions and preserves ratio");
            string artworkFile=Path.Combine(root,"hytcg-artworks",artId+".dat");byte[] container=File.ReadAllBytes(artworkFile);
            Check(!container.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),"artwork bytes are protected on disk");
            Check(economy.StoreCustomArtwork(original)==artId && Directory.GetFiles(Path.Combine(root,"hytcg-artworks"),"*.dat").Length==1,"normalized artwork deduplicates by SHA256");
            string[] concurrent=new string[8];Parallel.For(0,8,i=>concurrent[i]=economy.StoreCustomArtwork(original));Check(concurrent.All(id=>id==artId),"concurrent imports share one registered picture");
            LegacyStyle(economy,new HyTcgCosmetic {CardId=target.CardId,Frame="petal",Dye="#AABBCC",Morph="#223344",MorphMode="hue",ArtworkId=artId});
            Studio(economy,Input("cardId",target.CardId,"frame","petal","dye","#aabbcc","morph","#223344","morphMode","hue","artworkId",artId));
            var style=Items(Snapshot(economy),"Cosmetics").Single();Check((string)style["Dye"]=="#AABBCC" && (string)style["ArtworkId"]==artId && !style.ContainsKey("ArtworkData"),"studio validates colors and lazy artwork reference");
            Check(!Items(Snapshot(economy),"FrameItems").Any(x=>(bool)x["Available"]),"free legacy style migration binds its existing look but grants no unused frame inventory");
            HyTcgCosmetic copied=economy.GetCardCosmetic(target.CardId);copied.Frame="royal";copied.ArtworkId="";
            Check(economy.GetCardCosmetic(target.CardId).Frame=="petal" && economy.GetCardCosmetic(target.CardId).ArtworkId==artId,"renderer lookup clones style metadata");
            Check(economy.Snapshot().Gems==gems && parser.Serialize(economy.ReadOwnedCard(target.CardId))==identity,"studio leaves economy and immutable card identity unchanged");
            string ownedFile=Path.Combine(root,"cards",target.CardId+".hycard");byte[] ownedBytes=File.ReadAllBytes(ownedFile);
            CreateAlbum(economy,"Delete display only","paper");string deleteId=(string)Items(Snapshot(economy),"Albums").Last()["Id"];economy.CreativeAction("albumSave",Input("albumId",deleteId,"name","Delete display only","cardIds",new[]{target.CardId}));
            string temporaryAlbum=(string)Items(Snapshot(economy),"Albums").Last()["Id"];
            economy.CreativeAction("albumDelete",Input("albumId",temporaryAlbum));
            Check(Items(Snapshot(economy),"Albums").Length==1 && economy.GetCardCosmetic(target.CardId).ArtworkId==artId
                && File.ReadAllBytes(ownedFile).SequenceEqual(ownedBytes) && economy.Snapshot().Gems==gems,"album delete preserves owned card style bytes and gems");
            economy.CreativeAction("cosmeticReset",Input("cardId",target.CardId));
            Check(economy.GetCardCosmetic(target.CardId)==null && economy.ReadCustomArtwork(artId)==imageData
                && File.ReadAllBytes(artworkFile).SequenceEqual(container) && File.ReadAllBytes(ownedFile).SequenceEqual(ownedBytes)
                && parser.Serialize(economy.ReadOwnedCard(target.CardId))==identity && economy.Snapshot().Gems==gems,"style reset restores original display without consuming artwork card identity or gems");
            Studio(economy,Input("cardId",target.CardId,"restorePrevious",true));
            Error("FrameNotOwned",()=>Studio(economy,Input("cardId",target.CardId,"frame","evil")));
            Error("DyeInvalid",()=>Studio(economy,Input("cardId",target.CardId,"dye","red;url(http://x)")));
            Error("DyeInvalid",()=>Studio(economy,Input("cardId",target.CardId,"morphMode","multiply","morph","")));
            Error("CosmeticInvalid",()=>Studio(economy,Input("cardId",target.CardId,"artworkId","../outside")));
            Error("ArtworkUnavailable",()=>Studio(economy,Input("cardId",target.CardId,"artworkId",new string('0',64))));
            Error("ArtworkInvalid",()=>economy.ReadCustomArtwork("../outside"));
            Error("ArtworkInvalid",()=>economy.StoreCustomArtwork(new byte[8*1024*1024+1]));
            Error("ArtworkInvalid",()=>economy.StoreCustomArtwork(System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'></svg>")));
            byte[] oversized=(byte[])original.Clone();oversized[16]=0;oversized[17]=0;oversized[18]=16;oversized[19]=1;
            Error("ArtworkInvalid",()=>economy.StoreCustomArtwork(oversized));
            int jpegWidth,jpegHeight;byte[] jpegPng=HyTcgEconomy.NormalizeCustomArtwork(Picture(20,10,Color.Aqua,ImageFormat.Jpeg),out jpegWidth,out jpegHeight);
            Check(jpegPng[0]==137 && jpegWidth==20 && jpegHeight==10,"JPEG import canonicalizes to PNG");
            var restarted=new HyTcgEconomy(root,()=>now,new HyTcgUserProtector(),()=>TimeSpan.Zero);
            Check(Items(Snapshot(restarted),"Cosmetics").Length==1 && Items(Snapshot(restarted),"Albums").Length==1 && restarted.ReadCustomArtwork(artId)==imageData,"albums cosmetics artwork survive restart");
            byte[] tampered=(byte[])container.Clone();tampered[tampered.Length-1]^=1;File.WriteAllBytes(artworkFile,tampered);
            Error("ArtworkInvalid",()=>restarted.ReadCustomArtwork(artId));
            Check((string)Items(Snapshot(restarted),"Cosmetics").Single()["ArtworkId"]==artId,"unavailable custom image does not block creative snapshot");
            File.WriteAllBytes(artworkFile,container);
            using(var enlarged=new FileStream(artworkFile,FileMode.Open,FileAccess.Write))enlarged.SetLength(4*1024*1024+65536+1);
            Error("ArtworkInvalid",()=>restarted.ReadCustomArtwork(artId));File.WriteAllBytes(artworkFile,container);
            string otherRoot=Path.Combine(root,"other-account");var other=new HyTcgEconomy(otherRoot,()=>now,new HyTcgUserProtector(),()=>TimeSpan.Zero);
            Directory.CreateDirectory(Path.Combine(otherRoot,"hytcg-artworks"));File.Copy(artworkFile,Path.Combine(otherRoot,"hytcg-artworks",artId+".dat"));
            Error("ArtworkInvalid",()=>other.StoreCustomArtwork(original));Check(Items(Snapshot(other),"Cosmetics").Length==0,"artwork container is bound to its native account");
            var expedition=(HyTcgGameplayActionResult)restarted.GameplayAction("expeditionStart",Input("routeId","meadow","cardIds",new[]{target.CardId,material.CardId}));
            Check(restarted.GetCardCosmetic(target.CardId).ArtworkId==artId && Items(Snapshot(restarted),"Cosmetics").Length==1,"busy companions retain readable cosmetic metadata");
            restarted.CreativeAction("albumSave",Input("albumId",albumId,"name","Busy display","cardIds",new[]{material.CardId,target.CardId}));
            Check((string)Items(Snapshot(restarted),"Albums").Single()["Name"]=="Busy display","expedition team remains displayable in editable albums");
            Error("CardLocked",()=>Studio(restarted,Input("cardId",target.CardId)));
            Error("CardLocked",()=>restarted.CreativeAction("cosmeticReset",Input("cardId",target.CardId)));
            now=now.AddMinutes(5);
            restarted.GameplayAction("expeditionClaim",Input("expeditionId",expedition.Result.ExpeditionId));
            Check(restarted.GetCardCosmetic(target.CardId).Frame=="petal","mission claim preserves card studio style");
            var upgrade=restarted.PrepareUpgrade(target.CardId,new List<string>{material.CardId},false);
            string boundFrame=restarted.GetCardCosmetic(target.CardId).FrameItemId;
            Error("CardLocked",()=>restarted.CreativeAction("cosmeticReset",Input("cardId",target.CardId)));
            Error("CardLocked",()=>Studio(restarted,Input("cardId",material.CardId)));
            Error("CardLocked",()=>restarted.CreativeAction("albumSave",Input("albumId",albumId,"name","Locked","cardIds",new[]{target.CardId})));
            restarted.BeginUpgrade(upgrade.Token);restarted.AuthorizeUpgrade(upgrade.Token,upgrade.Card);
            string upgradedName=target.CardId+"-upgrade.hycard";byte[] upgradedBytes=HyTcgStorage.Protect(Picture(750,1050,Color.DarkSlateBlue,ImageFormat.Png));
            File.WriteAllBytes(Path.Combine(root,"cards",upgradedName),upgradedBytes);restarted.CompleteUpgrade(upgrade.Token,upgrade.Card,upgradedName,upgradedBytes);restarted.EndUpgrade(upgrade.Token);
            Check((string)Items(Snapshot(restarted),"Cosmetics").Single()["ArtworkId"]==artId && restarted.ReadOwnedCard(target.CardId).Revision==1,"upgrade retains studio style by stable card ID");
            Check(restarted.GetCardCosmetic(target.CardId).FrameItemId==boundFrame && boundFrame!="","upgrade retains the exact consumed frame instance binding");
            Check(restarted.ReadCustomArtwork(artId)==imageData && File.ReadAllBytes(artworkFile).SequenceEqual(container),"native upgrade retains exact registered custom artwork container and pixels");
            var upgradedSlots=Values(Items(Snapshot(restarted),"Albums").Single()["CardIds"]);
            Check((string)upgradedSlots[0]=="" && (string)upgradedSlots[1]==target.CardId,"consumed cards leave an empty album slot");
            restarted.Dissolve(target.CardId);
            Check(Items(Snapshot(restarted),"Cosmetics").Length==0 && Values(Items(Snapshot(restarted),"Albums").Single()["CardIds"]).All(id=>(string)id==""),"dissolved cards are hidden from styles and album displays");
            Error("CardUnavailable",()=>restarted.CreativeAction("cosmeticReset",Input("cardId",target.CardId)));
            Error("CardUnavailable",()=>restarted.GetCardCosmetic(target.CardId));
            restarted.ResetAccount("delete");restarted.FinishCreativeResetDeletes();
            Check(!File.Exists(artworkFile) && Items(Snapshot(restarted),"Albums").Length==0 && Items(Snapshot(restarted),"Cosmetics").Length==0,"account reset removes creative metadata and protected artwork");
            var limits=new HyTcgEconomy(Path.Combine(root,"limits"),()=>now,new HyTcgUserProtector(),()=>TimeSpan.Zero);
            for(int i=0;i<24;i++)CreateAlbum(limits,"Album "+i,"paper");
            Error("AlbumLimit",()=>CreateAlbum(limits,"Overflow","paper"));
            for(int i=0;i<64;i++)limits.StoreCustomArtwork(Picture(i+2,2,Color.FromArgb(255,i,20,40),ImageFormat.Png));
            Error("ArtworkLimit",()=>limits.StoreCustomArtwork(Picture(100,2,Color.FromArgb(255,100,20,40),ImageFormat.Png)));
            limits.ResetAccount("delete");limits.FinishCreativeResetDeletes();Check(Directory.GetFiles(Path.Combine(root,"limits","hytcg-artworks"),"*.dat").Length==0,"reset cleans all64 protected artwork files");
            Console.WriteLine("HYTCG creative verification PASS: "+checks+" checks (albums, native ownership, studio identity, protected imports, bounds, restart, upgrade locks, reset, limits).");return 0;
        }
        catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
