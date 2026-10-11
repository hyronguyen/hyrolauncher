using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using HYROLauncherWeb;

internal sealed class ShapeUpgradeTransport : IHyTcgTransport
{
    internal string ProfileData,ArtworkData;
    internal bool ZerochanFails,OriginalFails;
    internal int Searches;
    public string Json(string url,string agent,int timeout,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(url.Contains("animecharactersdatabase.com"))return "{\"id\":55,\"name\":\"Hinata Hyuuga\",\"origin\":\"Naruto\",\"gender\":\"Female\",\"character_image\":\"https://ami.animecharactersdatabase.com/images/55.jpg\"}";
        if(ZerochanFails)throw new IOException("fixture offline");
        if(url.Contains("l=50")){Searches++;return "{\"items\":[{\"id\":123}]}";}
        return "{\"full\":\"https://static.zerochan.net/new.123.png\",\"tags\":[\"Hinata Hyuuga\",\"Naruto\"]}";
    }
    public string Image(string url,string agent,int timeout,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(OriginalFails && url.Contains("original"))throw new IOException("fixture original unavailable");
        return url.Contains("new.123")?ArtworkData:ProfileData;
    }
}
internal static class VerifyHyTcgShapesUpgrades
{
    private static int checks;
    private static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    private static string Png(int w,int h)
    {
        using(var image=new Bitmap(w,h))using(var g=Graphics.FromImage(image))using(var output=new MemoryStream())
        {g.Clear(Color.DarkSlateBlue);image.Save(output,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(output.ToArray());}
    }
    private static void Throws(Action action,string code)
    {try{action();throw new Exception("Expected "+code);}catch(HyTcgException ex){Check(ex.Code==code,"Unexpected "+ex.Code);}}
    private static void Be32(byte[] b,int p,uint value)
    {b[p]=(byte)(value>>24);b[p+1]=(byte)(value>>16);b[p+2]=(byte)(value>>8);b[p+3]=(byte)value;}
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
            var config=new HyTcgConfig {CharacterIdMin=55,CharacterIdMax=55,MaxCharacterAttempts=15,Concurrency=2,RequestTimeoutSeconds=12,RetryDelayMs=100,ZerochanUsername=""};
            var transport=new ShapeUpgradeTransport {ProfileData=Png(640,400),ArtworkData=Png(300,500)};
            DateTime clock=DateTime.UtcNow;
            var wallet=new HyTcgEconomy(root,()=>clock,new HyTcgUserProtector(),()=>TimeSpan.Zero);wallet.FinishMigration();
            var service=new HyTcgService(root,transport,wallet);
            var opened=service.Open(config,(p,a)=>{},CancellationToken.None);
            Check(opened.Card.Orientation=="landscape" && opened.Card.ArtworkWidth==640 && opened.Card.ArtworkHeight==400,"Native landscape image dimensions");
            Check(HyTcgService.CanvasWidth(opened.Card)==1050 && HyTcgService.CanvasHeight(opened.Card)==750,"Landscape canonical dimensions");
            transport.ProfileData=Png(118,100);
            Check(service.Open(config,(p,a)=>{},CancellationToken.None).Card.Orientation=="landscape","Landscape ratio boundary");
            transport.ProfileData=Png(117,100);
            Check(service.Open(config,(p,a)=>{},CancellationToken.None).Card.Orientation=="portrait","Portrait ratio boundary");
            byte[] webp=new byte[30];System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(webp,0);System.Text.Encoding.ASCII.GetBytes("WEBPVP8X").CopyTo(webp,8);
            webp[24]=127;webp[25]=2;webp[27]=143;webp[28]=1;
            transport.ProfileData="data:image/webp;base64,"+Convert.ToBase64String(webp);
            var webpCard=service.Open(config,(p,a)=>{},CancellationToken.None).Card;
            Check(webpCard.Orientation=="landscape" && webpCard.ArtworkWidth==640 && webpCard.ArtworkHeight==400,"WebP native RIFF dimensions");
            byte[] avif=new byte[36];Be32(avif,0,16);System.Text.Encoding.ASCII.GetBytes("ftypavif").CopyTo(avif,4);Be32(avif,16,20);System.Text.Encoding.ASCII.GetBytes("ispe").CopyTo(avif,20);Be32(avif,28,640);Be32(avif,32,400);
            transport.ProfileData="data:image/avif;base64,"+Convert.ToBase64String(avif);
            var avifCard=service.Open(config,(p,a)=>{},CancellationToken.None).Card;
            Check(avifCard.Orientation=="landscape" && avifCard.ArtworkWidth==640 && avifCard.ArtworkHeight==400,"AVIF bounded spatial-extents dimensions");
            transport.ProfileData=Png(200,400);service.Fallback(opened.Card,config,CancellationToken.None);
            Check(opened.Card.Orientation=="portrait" && opened.Card.ArtworkWidth==200,"Profile fallback updates native frame");
            transport.ProfileData=Png(640,400);service.Resume(opened.Card,config,CancellationToken.None);
            Check(opened.Card.Orientation=="landscape","Recovery probes the retained source");
            opened.Card.SourceImage="https://static.zerochan.net/original.png";opened.Card.ImageSource="zerochan";
            opened.Card.Rarity="Common";opened.Card.RarityColor=HyTcgRules.RarityColor("Common");
            var target=opened.Card;var reservation=wallet.Reserve(1);wallet.Begin(reservation.Tokens[0]);wallet.AuthorizeCard(reservation.Tokens[0],target);
            string targetFile=service.SaveCard(target,Png(1050,750),reservation.Tokens[0]);
            Throws(()=>service.SaveCard(target,Png(750,1050),reservation.Tokens[0]),"CardInvalid");
            var material=service.Open(config,(p,a)=>{},CancellationToken.None).Card;material.Rarity="Uncommon";material.RarityColor=HyTcgRules.RarityColor("Uncommon");
            var materialReserve=wallet.Reserve(1);wallet.Begin(materialReserve.Tokens[0]);wallet.AuthorizeCard(materialReserve.Tokens[0],material);
            string materialFile=service.SaveCard(material,Png(1050,750),materialReserve.Tokens[0]);
            wallet.SetFavorite(target.CardId,true);
            var intent=wallet.PrepareUpgrade(target.CardId,new List<string>{material.CardId},false);wallet.BeginUpgrade(intent.Token);
            var keep=service.PrepareUpgrade(intent,config,CancellationToken.None);
            Check(keep.Card.SourceImage==target.SourceImage && keep.Card.Orientation==target.Orientation && transport.Searches==0,"Keep artwork does not query/reroll");
            Check(keep.Card.Rarity=="Uncommon" && keep.Card.Revision==1 && keep.Card.CardId==target.CardId,"Upgrade identity and next rarity");
            wallet.AuthorizeUpgrade(intent.Token,keep.Card);
            var upgraded=service.SaveUpgrade(keep.Card,Png(1050,750),intent.Token);
            Check(upgraded.ConsumedIds.SequenceEqual(new[]{material.CardId}) && upgraded.PreviousFilePath==targetFile,"Atomic native material consumption");
            Check(File.Exists(targetFile) && File.Exists(materialFile) && upgraded.FilePath!=targetFile,"Old files retained until caller cleanup");
            using(var stream=new MemoryStream(HyTcgStorage.ImageBytes(File.ReadAllBytes(upgraded.FilePath))))using(var image=Image.FromStream(stream))Check(image.Width==1050 && image.Height==750,"Landscape upgraded encrypted PNG");
            Check(service.Collection(0,"","",true).Total==1 && service.Collection(0,"Uncommon","Hinata",true).Items[0].Card.Revision==1,"Favorite and filters survive upgrade");
            Check(!service.Collection(0,"","").Items.Any(i=>i.Card.CardId==material.CardId),"Consumed materials leave collection");
            var again=service.SaveUpgrade(keep.Card,Png(1050,750),intent.Token);
            Check(again.FilePath==upgraded.FilePath && again.Wallet.Gems==1800,"Idempotent upgrade save");
            var nextMaterial=service.Open(config,(p,a)=>{},CancellationToken.None).Card;nextMaterial.Rarity="Rare";nextMaterial.RarityColor=HyTcgRules.RarityColor("Rare");
            var nextReserve=wallet.Reserve(1);wallet.Begin(nextReserve.Tokens[0]);wallet.AuthorizeCard(nextReserve.Tokens[0],nextMaterial);service.SaveCard(nextMaterial,Png(1050,750),nextReserve.Tokens[0]);
            var changeIntent=wallet.PrepareUpgrade(target.CardId,new List<string>{nextMaterial.CardId},true);wallet.BeginUpgrade(changeIntent.Token);
            config.ZerochanUsername="HYTCG-Test";
            var changed=service.PrepareUpgrade(changeIntent,config,CancellationToken.None);
            Check(changed.Card.SourceImage.Contains("new.123") && changed.Card.Orientation=="portrait" && changed.Card.ArtworkWidth==300,"Optional new artwork gets its own native orientation");
            wallet.AuthorizeUpgrade(changeIntent.Token,changed.Card);
            var pending=wallet.PendingUpgrades().Single();int searches=transport.Searches;
            var resumed=service.PrepareUpgrade(pending,config,CancellationToken.None);
            Check(resumed.Card.SourceImage==changed.Card.SourceImage && transport.Searches==searches,"Authorized upgrade recovery cannot reroll artwork");
            wallet.CancelUpgrade(changeIntent.Token);
            var fallbackIntent=wallet.PrepareUpgrade(target.CardId,new List<string>{nextMaterial.CardId},true);wallet.BeginUpgrade(fallbackIntent.Token);transport.ZerochanFails=true;
            var retained=service.PrepareUpgrade(fallbackIntent,config,CancellationToken.None);
            Check(retained.Card.SourceImage==target.SourceImage && retained.Card.Orientation=="landscape","Optional artwork API outage retains exact prior source/frame");
            transport.ZerochanFails=false;
            byte[] oversized=(byte[])webp.Clone();oversized[24]=63;oversized[25]=31;oversized[27]=87;oversized[28]=27;
            transport.ArtworkData="data:image/webp;base64,"+Convert.ToBase64String(oversized);
            var bounded=service.PrepareUpgrade(fallbackIntent,config,CancellationToken.None);
            Check(bounded.Card.SourceImage==target.SourceImage && bounded.Card.ArtworkWidth==640 && bounded.ImageData==transport.ProfileData,"Oversized optional artwork retains existing artwork before authorization");
            transport.ArtworkData="data:image/png;base64,fixture";
            var malformed=service.PrepareUpgrade(fallbackIntent,config,CancellationToken.None);
            Check(malformed.Card.SourceImage==target.SourceImage && malformed.ImageData==transport.ProfileData,"Malformed optional PNG cannot replace existing artwork");
            transport.ArtworkData=Png(300,500);
            wallet.EndUpgrade(fallbackIntent.Token);wallet.CancelUpgrade(fallbackIntent.Token);
            var failIntent=wallet.PrepareUpgrade(target.CardId,new List<string>{nextMaterial.CardId},false);wallet.BeginUpgrade(failIntent.Token);transport.OriginalFails=true;
            try{service.PrepareUpgrade(failIntent,config,CancellationToken.None);throw new Exception("Original failure was hidden");}catch(IOException){}
            wallet.EndUpgrade(failIntent.Token);wallet.CancelUpgrade(failIntent.Token);
            Check(wallet.ReadOwnedCard(target.CardId).Rarity=="Uncommon" && wallet.ReadOwnedCard(nextMaterial.CardId)!=null,"Artwork failure consumes nothing");
            var abandoned=wallet.PrepareUpgrade(target.CardId,new List<string>{nextMaterial.CardId},false);
            string orphan=Path.Combine(service.CardsDirectory,"Rare_Hinata_Hyuuga_"+target.CardId.Substring(3)+"_R2_"+abandoned.Token.Substring(3)+".png");
            File.WriteAllBytes(orphan,Convert.FromBase64String(Png(1050,750).Substring("data:image/png;base64,".Length)));
            wallet.CancelUpgrade(abandoned.Token);
            var replacement=wallet.PrepareUpgrade(target.CardId,new List<string>{nextMaterial.CardId},true);wallet.BeginUpgrade(replacement.Token);
            transport.OriginalFails=false;transport.ZerochanFails=false;
            var replacementArt=service.PrepareUpgrade(replacement,config,CancellationToken.None);wallet.AuthorizeUpgrade(replacement.Token,replacementArt.Card);
            var replacementSaved=service.SaveUpgrade(replacementArt.Card,Png(750,1050),replacement.Token);
            Check(replacementSaved.FilePath!=orphan && File.Exists(orphan),"New intent nonce avoids canceled/crashed orphan collisions");
            Check(wallet.ReadOwnedCard(target.CardId).Rarity=="Rare" && wallet.ReadOwnedCard(target.CardId).Orientation=="portrait","Portrait reframe commits with new rarity");
            Console.WriteLine("PASS: "+checks+" native shape, canonical PNG, upgrade artwork/recovery, atomic save/material, and favorite-filter checks.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
