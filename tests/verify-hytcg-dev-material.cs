using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal static class VerifyHyTcgDevMaterial
{
    private static int checks;
    private static void Check(bool value,string message) {checks++;if(!value)throw new Exception(message);}
    private static byte[] Bytes(string data) {return Convert.FromBase64String(data.Substring(data.IndexOf(',')+1));}
    private static string Picture(int width,int height)
    {
        using(var image=new Bitmap(width,height))using(var graphics=Graphics.FromImage(image))using(var stream=new MemoryStream())
        {graphics.Clear(Color.FromArgb(45,110,170));image.Save(stream,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());}
    }
    private static Bitmap Bitmap(byte[] bytes)
    {using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream,true,true))return new Bitmap(image);}
    private static bool SameBox(Bitmap left,Bitmap right,Rectangle box)
    {for(int y=box.Top;y<box.Bottom;y++)for(int x=box.Left;x<box.Right;x++)if(left.GetPixel(x,y).ToArgb()!=right.GetPixel(x,y).ToArgb())return false;return true;}
    private static bool SameLightInk(Bitmap left,Bitmap right,Rectangle box)
    {
        int ink=0,differences=0;
        for(int y=box.Top;y<box.Bottom;y++)for(int x=box.Left;x<box.Right;x++)
        {Color a=left.GetPixel(x,y),b=right.GetPixel(x,y);bool one=a.R>110 && a.G>110,two=b.R>110 && b.G>110;if(two)ink++;if(one!=two)differences++;}
        return ink>100 && differences<=Math.Max(5,ink/100);
    }
    private static Dictionary<string,string> Hashes(string root)
    {
        using(var sha=SHA256.Create())return Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(file=>file.Substring(root.Length),file=>Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(file))),StringComparer.Ordinal);
    }
    private static HyTcgCard Issue(HyTcgEconomy economy,string root,string rarity,bool landscape,int index)
    {
        string token=economy.Reserve(1,"gems").Tokens[0];economy.Begin(token);
        var card=new HyTcgCard {CharacterId=90000+index,CardId="HT-20261011-"+index.ToString("X12"),Name="Material fixture "+index,Series="Local verification",Gender="Female",
            Rarity=rarity,RarityColor=HyTcgRules.RarityColor(rarity),Orientation=landscape?"landscape":"portrait",ImageSource="zerochan",SourceImage="https://static.zerochan.net/fixture.png",ProfileImage="https://www.animecharactersdatabase.com/fixture.png",CreatedAt=DateTime.UtcNow.ToString("o"),Metadata=new Dictionary<string,string> {{"description","A local artwork fixture."}}};
        economy.AuthorizeCard(token,card);
        int width=HyTcgService.CanvasWidth(card),height=HyTcgService.CanvasHeight(card);
        string art=Picture(landscape?588:645,landscape?668:620),canonical=Picture(width,height);
        string rendered=HyTcgCosmeticRenderer.Render(card,canonical,null,art,false,null,null,true);
        byte[] container=HyTcgStorage.Protect(HyTcgService.Png.WithMetadata(Bytes(rendered),new JavaScriptSerializer().Serialize(card)));
        string name="fixture-"+index+".hycard";Directory.CreateDirectory(Path.Combine(root,"cards"));File.WriteAllBytes(Path.Combine(root,"cards",name),container);
        economy.Complete(token,card,name,container);return card;
    }
    // A genuine old, native-signed landscape fixture exercises extraction of the
    // historical artwork box. New authorizations correctly refuse to mint it.
    private static HyTcgCard HistoricalLandscape(HyTcgEconomy economy,string root)
    {
        var card=new HyTcgCard {CharacterId=90006,CardId="HT-20261011-000000000006",Name="Historical material fixture",Series="Local verification",Gender="Female",Rarity="SR",RarityColor=HyTcgRules.RarityColor("SR"),Orientation="landscape",ImageSource="zerochan",SourceImage="https://static.zerochan.net/fixture.png",ProfileImage="https://www.animecharactersdatabase.com/fixture.png",CreatedAt=DateTime.UtcNow.ToString("o"),Metadata=new Dictionary<string,string>{{"description","A local artwork fixture."},{"hytcg_card_layout","window-v3"}}};
        byte[] png;using(var image=new Bitmap(1050,750))using(var graphics=Graphics.FromImage(image))using(var output=new MemoryStream())
        {
            graphics.Clear(Color.FromArgb(238,235,224));
            using(var brush=new SolidBrush(Color.FromArgb(45,110,170)))graphics.FillRectangle(brush,42,33.75f,588,667.5f);
            using(var font=new Font("Segoe UI",14))graphics.DrawString(card.CardId,font,Brushes.Black,682,657);
            image.Save(output,ImageFormat.Png);png=output.ToArray();
        }
        byte[] container=HyTcgStorage.Protect(HyTcgService.Png.WithMetadata(png,new JavaScriptSerializer().Serialize(card)));
        string name="historical-landscape.hycard";Directory.CreateDirectory(Path.Combine(root,"cards"));File.WriteAllBytes(Path.Combine(root,"cards",name),container);
        MethodInfo write=typeof(HyTcgEconomy).GetMethod("Write",BindingFlags.Instance|BindingFlags.NonPublic).MakeGenericMethod(typeof(bool));
        Type signature=write.GetParameters()[0].ParameterType,vaultType=signature.GetGenericArguments()[0];
        var vault=Expression.Parameter(vaultType,"vault");var time=Expression.Parameter(typeof(DateTime),"time");
        var call=Expression.Call(typeof(VerifyHyTcgDevMaterial).GetMethod("AddHistorical",BindingFlags.Static|BindingFlags.NonPublic),Expression.Convert(vault,typeof(object)),Expression.Constant(card),Expression.Constant(name),Expression.Constant(container));
        write.Invoke(economy,new object[]{Expression.Lambda(signature,call,vault,time).Compile()});return card;
    }
    private static bool AddHistorical(object vault,HyTcgCard card,string name,byte[] container)
    {
        object owned=typeof(HyTcgEconomy).GetMethod("CreateOwned",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{vault,card,name,container,false});
        ((IList)vault.GetType().GetProperty("Cards").GetValue(vault,null)).Add(owned);return true;
    }
    private static void Verify(HyTcgEconomy dev,string root,HyTcgCard original)
    {
        string id=dev.EnsureDevUpgradeMaterial(original.CardId);HyTcgCard card=dev.ReadOwnedCard(id);
        string next=HyTcgService.Rarities[HyTcgRules.RarityIndex(original.Rarity)+1];bool landscape=original.Orientation=="landscape";
        Check(card.Rarity==next && card.RarityColor==HyTcgRules.RarityColor(next),"Material rarity and native ink are wrong");
        Check(card.CharacterId==original.CharacterId && card.Name==original.Name && card.Series==original.Series,"Material character changed");
        Check(card.CardId!=original.CardId && card.Revision==0,"Material did not get its own identity");
        Check(card.Orientation=="portrait","Material must use portrait orientation even for a historical landscape target");
        Check(card.Metadata["hytcg_card_layout"]==(HyTcgRules.RarityIndex(next)<=3?"window-v4":"full-v4"),"Material layout marker wrong");
        var item=dev.CollectionCards().Single(c=>c.Card.CardId==id);
        Check(item.CanDissolve && !item.IsLegacy,"Native signature/ownership verification failed");
        byte[] container=File.ReadAllBytes(item.ImagePath),png=HyTcgStorage.ImageBytes(container);
        using(var actual=Bitmap(png))
        {
            int width=750,height=1050;
            Check(actual.Width==width && actual.Height==height,"Material must export 750x1050 without changing the source card");
            string source=Picture(landscape?588:645,landscape?668:620);
            string reference=HyTcgCosmeticRenderer.Render(card,Picture(width,height),null,source,false,null,null,true);
            using(var expected=Bitmap(Bytes(reference)))
            {
                Check(SameBox(actual,expected,new Rectangle(4,200,20,180)),"New rarity border was not rendered");
                Rectangle identity=HyTcgRules.RarityIndex(next)<=3?new Rectangle(60,989,620,24):new Rectangle(52,height-56,width-104,20);
                Check(HyTcgRules.RarityIndex(next)<=3?SameBox(actual,expected,identity):SameLightInk(actual,expected,identity),"Material keeps old baked CardId pixels: "+original.Rarity);
                if(original.Rarity=="SR")
                {
                    Color pixel=actual.GetPixel(width/2,730);
                    Check(pixel.B>pixel.R && pixel.R<100,"SR to SSR retains low-tier cream footer instead of full artwork");
                }
                else if(HyTcgRules.RarityIndex(next)<=3)
                {
                    Rectangle rarity=new Rectangle(573,696,121,37);
                    Check(SameBox(actual,expected,rarity),"Low rarity material keeps old rarity text");
                }
            }
        }
        Check(dev.EnsureDevUpgradeMaterial(original.CardId)==id,"Material retry duplicated native inventory");
        byte[] corrupt=(byte[])container.Clone();corrupt[corrupt.Length-1]^=1;File.WriteAllBytes(item.ImagePath,corrupt);
        Check(!dev.CanDissolve(id),"Material corruption bypassed signed ownership");File.WriteAllBytes(item.ImagePath,container);
        Check(dev.CanDissolve(id),"Original signed material did not recover after local QA corruption");
        var intent=dev.PrepareUpgrade(original.CardId,new List<string>{id},false);
        Check(intent.Card.Rarity==next && intent.Card.CharacterId==original.CharacterId,"Generated material is unusable in normal upgrade validation");dev.CancelUpgrade(intent.Token);
        File.WriteAllBytes(Path.Combine(root,original.Rarity+"-to-"+next+(landscape?"-landscape":"-portrait")+".png"),png);
    }
    private static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]),sourceRoot=Path.Combine(root,"real-fixture"),devRoot=Path.Combine(root,"dev-fixture");Directory.CreateDirectory(sourceRoot);
            var source=new HyTcgEconomy(sourceRoot);var cards=new[] {Issue(source,sourceRoot,"Common",false,1),Issue(source,sourceRoot,"Rare",true,2),Issue(source,sourceRoot,"SR",false,3),Issue(source,sourceRoot,"SR",true,4),Issue(source,sourceRoot,"SSR",false,5),HistoricalLandscape(source,sourceRoot)};
            Check(cards.Take(5).All(card=>card.Orientation=="portrait") && cards[5].Orientation=="landscape","new authorizations are portrait while signed historical landscape metadata remains unchanged");
            var before=Hashes(sourceRoot);var dev=source.CreateDevSandbox(devRoot);
            foreach(var card in cards)Verify(dev,root,card);
            Check(before.OrderBy(p=>p.Key).SequenceEqual(Hashes(sourceRoot).OrderBy(p=>p.Key)),"Material creation mutated real account/card files");
            Check(dev.CollectionCards().Count==12 && source.CollectionCards().Count==6,"Temporary generated materials leaked into real inventory");
            var after=Hashes(sourceRoot);dev.CloseDevMode();
            try{dev.EnsureDevUpgradeMaterial(cards[0].CardId);throw new Exception("Closed dev still creates material");}catch(HyTcgException ex){Check(ex.Code=="DevExpired","Closed dev error wrong");}
            Check(after.OrderBy(p=>p.Key).SequenceEqual(Hashes(sourceRoot).OrderBy(p=>p.Key)),"Closing dev mutated real account");
            var report=new {Status="PASS",Checks=checks,Cases=new[]{"Common-Uncommon portrait","Rare-SR wide input normalized portrait","SR-SSR portrait and genuine signed historical landscape","SSR-UR portrait","signed ownership and tamper rejection","real account unchanged","closed dev rejected","no external API or account access"},TestRoot=root};
            string json=new JavaScriptSerializer().Serialize(report);File.WriteAllText(Path.Combine(root,"report.json"),json);Console.WriteLine(json);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
