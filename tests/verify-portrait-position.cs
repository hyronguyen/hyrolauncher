using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

internal static class PortraitPositionVerification
{
    private static readonly BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private static readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=64*1024*1024};
    private static readonly DateTime now=new DateTime(2026,10,11,12,0,0,DateTimeKind.Utc);
    private static Assembly app;
    private static Type economyType,cardType,styleType,rendererType,storageType,pngType;
    private static int checks,cardSequence;
    private static readonly List<string> softFailures=new List<string>();
    private static object Invoke(Type type,object target,string method,params object[] args)
    {
        var match=type.GetMethods(flags).Where(m=>m.Name==method&&m.GetParameters().Length==args.Length)
            .FirstOrDefault(m=>m.GetParameters().Select((p,i)=>args[i]==null||p.ParameterType.IsInstanceOfType(args[i])).All(x=>x));
        if(match==null)throw new Exception("Missing candidate method "+type.Name+"."+method);
        try{return match.Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException;}
    }
    private static object Call(object target,string method,params object[] args){return Invoke(target.GetType(),target,method,args);}
    private static object Static(Type type,string method,params object[] args){return Invoke(type,null,method,args);}
    private static object Get(object target,string name){return target.GetType().GetProperty(name,flags).GetValue(target,null);}
    private static void Set(object target,string name,object value){target.GetType().GetProperty(name,flags).SetValue(target,value,null);}
    private static void Check(bool okay,string name){checks++;if(!okay)throw new Exception(name);}
    private static void Soft(bool okay,string name){checks++;if(!okay)softFailures.Add(name);}
    private static void Fails(string code,Action action)
    {
        try{action();throw new Exception("Expected native "+code);}catch(Exception e)
        {if(e.GetType().Name!="HyTcgException"||(string)e.GetType().GetField("Code",flags).GetValue(e)!=code)throw;checks++;}
    }
    private static Dictionary<string,object> Input(params object[] pairs)
    {var input=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)input.Add((string)pairs[i],pairs[i+1]);return input;}
    private static object Economy(string root)
    {
        Directory.CreateDirectory(root);var protector=Activator.CreateInstance(app.GetType("HYROLauncherWeb.HyTcgUserProtector"));
        return Activator.CreateInstance(economyType,flags,null,new object[]{root,new Func<DateTime>(delegate{return now;}),protector,new Func<TimeSpan>(delegate{return TimeSpan.Zero;})},CultureInfo.InvariantCulture);
    }
    private static string Operation(object economy){return "SC-"+Get(Call(economy,"CreativeSnapshot"),"NextStudioSequence")+"-"+Guid.NewGuid().ToString("N");}
    private static object Card(string rarity,string orientation,string marker)
    {
        var card=Activator.CreateInstance(cardType);string id="HT-20261011-"+(++cardSequence).ToString("X12");
        Set(card,"CardId",id);Set(card,"CharacterId",99991);Set(card,"Name","Portrait test "+cardSequence);Set(card,"Series","HYTCG native fixture");Set(card,"Gender","Female");
        Set(card,"Rarity",rarity);Set(card,"RarityColor",(string)Static(app.GetType("HYROLauncherWeb.HyTcgRules"),"RarityColor",rarity));Set(card,"Orientation",orientation);
        Set(card,"ImageSource","zerochan");Set(card,"SourceImage","https://www.zerochan.net/fixture.jpg");Set(card,"ProfileImage","https://ami.animecharactersdatabase.com/fixture.jpg");
        Set(card,"CreatedAt",now.ToString("o"));Set(card,"Metadata",new Dictionary<string,string>{{"hytcg_card_layout",marker},{"description","Complete original character artwork."}});return card;
    }
    private static object Style(object card,double scale,double x,double y,string dye,bool mystic)
    {
        var style=Activator.CreateInstance(styleType);Set(style,"CardId",Get(card,"CardId"));Set(style,"Frame","classic");Set(style,"FrameItemId","");Set(style,"Dye",dye);Set(style,"DyeId","");
        Set(style,"Morph","");Set(style,"MorphMode","none");Set(style,"ArtworkId","");Set(style,"Mystic",mystic);Set(style,"ArtworkScale",scale);Set(style,"ArtworkX",x);Set(style,"ArtworkY",y);return style;
    }
    private static string Png(int width,int height,bool patterned)
    {
        using(var pixels=new Bitmap(width,height))using(var graphics=Graphics.FromImage(pixels))using(var stream=new MemoryStream())
        {graphics.Clear(Color.FromArgb(246,239,226));if(patterned){graphics.FillRectangle(Brushes.Red,0,0,width,height/3);graphics.FillRectangle(Brushes.Lime,0,height/3,width,height/3);graphics.FillRectangle(Brushes.Blue,0,height*2/3,width,height-height*2/3);graphics.FillRectangle(Brushes.Gold,width/3,0,width/3,height);}
            pixels.Save(stream,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());}
    }
    private static Bitmap Pixels(string value)
    {using(var stream=new MemoryStream(Convert.FromBase64String(value.Substring(value.IndexOf(',')+1))))using(var image=Image.FromStream(stream)){return new Bitmap(image);}}
    private static byte[] Bytes(string value){return Convert.FromBase64String(value.Substring(value.IndexOf(',')+1));}
    private static string Render(object card,string canonical,object style,string artwork,bool thumbnail)
    {return (string)Static(rendererType,"Render",card,canonical,style,artwork,thumbnail,null,null,true);}
    private static string Artwork=Png(160,240,true);
    private static object Mint(object economy,string root,string rarity)
    {
        var card=Card(rarity,"landscape","old-test-layout");
        Static(app.GetType("HYROLauncherWeb.HyTcgService"),"SetArtworkShape",card,Png(400,200,true));
        Check((string)Get(card,"Orientation")=="portrait" && (int)Get(card,"ArtworkWidth")==400 && (int)Get(card,"ArtworkHeight")==200,"Fetched horizontal artwork remains portrait while retaining source dimensions: "+rarity);
        string mode=(int)Get(Call(economy,"Snapshot"),"FreeCredits")>0?"credits":"gems";
        var reserve=Call(economy,"Reserve",1,mode);string token=((List<string>)Get(reserve,"Tokens"))[0];Call(economy,"Begin",token);Call(economy,"AuthorizeCard",token,card);
        Check((string)Get(card,"Orientation")=="portrait","Authorized caller is portrait: "+rarity);
        string marker=((Dictionary<string,string>)Get(card,"Metadata"))["hytcg_card_layout"];
        Check(marker==(rarity=="SSR"||rarity=="UR"?"full-v4":"window-v4"),"Authorized native layout marker: "+rarity);
        var pending=((IEnumerable)Call(economy,"PendingCards")).Cast<object>().Single(p=>(string)Get(Get(p,"Card"),"CardId")== (string)Get(card,"CardId"));
        Check((string)Get(Get(pending,"Card"),"Orientation")=="portrait","Recovery renders portrait: "+rarity);
        string rendered=Render(card,Png(750,1050,false),null,Artwork,false);
        using(var image=Pixels(rendered))Check(image.Width==750&&image.Height==1050,"Native issued renderer portrait pixels: "+rarity);
        Commit(economy,root,token,card,rendered,false);return card;
    }
    private static string Commit(object economy,string root,string token,object card,string image,bool upgrade)
    {
        string file=(string)Get(card,"CardId")+"-r"+Get(card,"Revision")+".hycard";
        byte[] png=(byte[])Static(pngType,"WithMetadata",Bytes(image),json.Serialize(card));byte[] container=(byte[])Static(storageType,"Protect",png);
        Directory.CreateDirectory(Path.Combine(root,"cards"));File.WriteAllBytes(Path.Combine(root,"cards",file),container);
        Call(economy,upgrade?"CompleteUpgrade":"Complete",token,card,file,container);return file;
    }
    private static string Hash(byte[] value){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(value));}
    private static string HashFile(string path){return Hash(File.ReadAllBytes(path));}
    private static string ImageHash(Bitmap image,Rectangle rectangle)
    {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){for(int y=rectangle.Top;y<rectangle.Bottom;y++)for(int x=rectangle.Left;x<rectangle.Right;x++)writer.Write(image.GetPixel(x,y).ToArgb());return Hash(stream.ToArray());}}
    private static object Preview(object economy,string id,Dictionary<string,object> position)
    {return Call(economy,"ReadLifestylePreview",id,"classic","",null,false,false,"",null,"landscape",position);}
    private static void NewPortraitAndUpgrade(string root)
    {
        var economy=Economy(root);var cards=new List<object>();
        foreach(string rarity in new[]{"Common","Uncommon","Rare","SR","SSR","UR"})cards.Add(Mint(economy,root,rarity));
        object target=cards[0];string id=(string)Get(target,"CardId");Call(economy,"SetFavorite",id,true);
        for(int i=1;i<cards.Count;i++)
        {
            var intent=Call(economy,"PrepareUpgrade",id,new List<string>{(string)Get(cards[i],"CardId")},false);var updated=Get(intent,"Card");string token=(string)Get(intent,"Token");
            Check((string)Get(updated,"Orientation")=="portrait","Upgrade keeps portrait at "+Get(updated,"Rarity"));
            Check(((Dictionary<string,string>)Get(updated,"Metadata"))["hytcg_card_layout"]==(i>=4?"full-v4":"window-v4"),"Upgrade determines new rarity frame marker "+i);
            Call(economy,"BeginUpgrade",token);Call(economy,"AuthorizeUpgrade",token,updated);
            string image=Render(updated,Png(750,1050,false),null,Artwork,false);Commit(economy,root,token,updated,image,true);
            target=Call(economy,"ReadOwnedCard",id);Check((int)Get(target,"Revision")==i&&(string)Get(target,"Orientation")=="portrait","Signed completed upgrade portrait revision "+i);
        }
        // Direct native callers/recovered state also need the normalized orientation,
        // even if source-shape detection was skipped by the caller.
        var direct=Card("Common","landscape","legacy-window");var reserve=Call(economy,"Reserve",1,"gems");string pull=((List<string>)Get(reserve,"Tokens"))[0];
        Call(economy,"Begin",pull);Call(economy,"AuthorizeCard",pull,direct);
        var recovery=((IEnumerable)Call(economy,"PendingCards")).Cast<object>().Single(p=>(string)Get(Get(p,"Card"),"CardId")== (string)Get(direct,"CardId"));
        Check((string)Get(Get(recovery,"Card"),"Orientation")=="portrait","Native authority normalizes stored issued landscape input");
        Soft((string)Get(direct,"Orientation")=="portrait","AuthorizeCard normalizes issued copy but does not copy Orientation back to the caller; a caller skipping SetArtworkShape renders landscape while the signed pending model is portrait");
    }
    private static void LegacyLandscape(string root)
    {
        var economy=Economy(root);var card=Mint(economy,root,"UR");string id=(string)Get(card,"CardId"),file=Path.Combine(root,"cards",id+"-r0.hycard");
        var protector=Activator.CreateInstance(app.GetType("HYROLauncherWeb.HyTcgUserProtector"));string vaultFile=Path.Combine(root,"hytcg-vault.dat");
        Type vaultType=economyType.GetNestedType("Vault",BindingFlags.NonPublic);var vault=json.Deserialize(Encoding.UTF8.GetString((byte[])Call(protector,"Unprotect",File.ReadAllBytes(vaultFile))),vaultType);
        var owned=((IEnumerable)Get(vault,"Cards")).Cast<object>().Single();card=Get(owned,"Card");Set(card,"Orientation","landscape");Set(card,"Metadata",new Dictionary<string,string>{{"hytcg_card_layout","full-v3"},{"description","Original horizontal signed card."}});
        byte[] png=(byte[])Static(pngType,"WithMetadata",Bytes(Png(1050,750,true)),json.Serialize(card));byte[] container=(byte[])Static(storageType,"Protect",png);File.WriteAllBytes(file,container);
        var replacement=Static(economyType,"CreateOwned",vault,card,Path.GetFileName(file),container,false);((IList)Get(vault,"Cards"))[0]=replacement;
        File.WriteAllBytes(vaultFile,(byte[])Call(protector,"Protect",Encoding.UTF8.GetBytes(json.Serialize(vault))));
        economy=Economy(root);string fileBefore=HashFile(file),vaultBefore=HashFile(vaultFile),model=json.Serialize(Call(economy,"ReadOwnedCard",id));
        using(var image=Pixels((string)Call(economy,"ReadLifestyleImage",id,false)))Check(image.Width==750&&image.Height==1050,"Legacy signed landscape detail displays portrait");
        using(var image=Pixels((string)Call(economy,"ReadLifestyleImage",id,true)))Check(image.Width*7==image.Height*5,"Legacy signed landscape thumbnail displays portrait");
        using(var image=Pixels((string)Get(Preview(economy,id,Input("artworkScale",2d,"artworkX",.4d,"artworkY",-.2d)),"imageData")))Check(image.Width==750&&image.Height==1050,"Legacy preview ignores obsolete landscape switch and positions within portrait");
        using(var image=Pixels((string)Call(economy,"ReadCardImage",id)))Check(image.Width==1050&&image.Height==750,"Canonical legacy image stays horizontal and authenticated");
        Check(json.Serialize(Call(economy,"ReadOwnedCard",id))==model,"Legacy ownership metadata/fingerprint model untouched by display");
        Check(HashFile(file)==fileBefore&&HashFile(vaultFile)==vaultBefore,"Legacy display/preview does not alter protected image or vault bytes");
    }
    private static void PositionContract(string root)
    {
        var economy=Economy(root);var card=Mint(economy,root,"Common");string id=(string)Get(card,"CardId"),vaultFile=Path.Combine(root,"hytcg-vault.dat"),cardFile=Path.Combine(root,"cards",id+"-r0.hycard");
        var defaults=Call(economy,"CreativeQuote",Input("cardId",id,"artworkScale",1d,"artworkX",0d,"artworkY",0d));
        Check((bool)Get(defaults,"NoOp")&&(int)Get(defaults,"TotalGems")==0&&!(bool)Get(defaults,"DyeConsumed"),"Explicit default position is a free NoOp");
        foreach(string field in new[]{"artworkScale","artworkX","artworkY"})
        {
            object[] invalid=field=="artworkScale"?new object[]{Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity,"2",true,null,0d,.9999d,4.0001d,Double.MaxValue,new object()}:new object[]{Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity,"0",false,null,-1.0001d,1.0001d,Double.MaxValue,new object()};
            foreach(object raw in invalid)
            {
                string before=HashFile(vaultFile);var input=Input("cardId",id,field,raw);
                Fails("CosmeticInvalid",delegate{Call(economy,"CreativeQuote",input);});
                Fails("CosmeticInvalid",delegate{Preview(economy,id,Input(field,raw));});
                input["operationId"]=Operation(economy);Fails("CosmeticInvalid",delegate{Call(economy,"CreativeAction","cosmeticSave",input);});
                Check(HashFile(vaultFile)==before,"Invalid position read/preview/write leaves vault bytes untouched: "+field+"/"+(raw==null?"null":raw.GetType().Name));
            }
        }
        foreach(double scale in new[]{1d,4d})foreach(double x in new[]{-1d,1d})foreach(double y in new[]{-1d,1d})
        {var quote=Call(economy,"CreativeQuote",Input("cardId",id,"artworkScale",scale,"artworkX",x,"artworkY",y));Check((int)Get(quote,"TotalGems")==0,"Position boundary accepted without art cost");}
        string fileBefore=HashFile(cardFile);int gems=(int)Get(Call(economy,"Snapshot"),"Gems");var move=Input("operationId",Operation(economy),"cardId",id,"artworkScale",1.123456d,"artworkX",.456789d,"artworkY",-.234567d);
        var quoteMove=Call(economy,"CreativeQuote",move);Check(!(bool)Get(quoteMove,"NoOp")&&(int)Get(quoteMove,"TotalGems")==0&&!(bool)Get(quoteMove,"DyeConsumed"),"Position-only quote is a change with no payment or dye consumption");
        var saved=Call(economy,"CreativeAction","cosmeticSave",move);var receipt=Get(saved,"LastStudioReceipt");Check((int)Get(receipt,"GemsSpent")==0&&!(bool)Get(receipt,"NoOp"),"Position-only receipt records zero gems");
        var position=Call(economy,"GetCardCosmetic",id);Check((double)Get(position,"ArtworkScale")==1.1235d&&(double)Get(position,"ArtworkX")==.4568d&&(double)Get(position,"ArtworkY")==-.2346d,"Native position rounds to four decimals");
        Call(economy,"CreativeAction","cosmeticSave",move);Check((int)Get(Call(economy,"Snapshot"),"Gems")==gems,"Replaying movement cannot debit gems");
        var altered=new Dictionary<string,object>(move);altered["artworkX"]=-.5d;Fails("StudioReplayMismatch",delegate{Call(economy,"CreativeAction","cosmeticSave",altered);});
        Check((bool)Get(Call(economy,"CreativeQuote",move),"NoOp"),"Existing same rounded position returns NoOp");
        var same=new Dictionary<string,object>(move);same["operationId"]=Operation(economy);var sameSave=Call(economy,"CreativeAction","cosmeticSave",same);Check((bool)Get(Get(sameSave,"LastStudioReceipt"),"NoOp"),"Repeated position with new intent is NoOp");
        economy=Economy(root);position=Call(economy,"GetCardCosmetic",id);Check((double)Get(position,"ArtworkScale")==1.1235d&&(double)Get(position,"ArtworkX")==.4568d&&(double)Get(position,"ArtworkY")==-.2346d,"Position persists after native account reload");
        Check(HashFile(cardFile)==fileBefore&&json.Serialize(Call(economy,"ReadOwnedCard",id))==json.Serialize(card),"Position updates neither signed card identity nor canonical file");
        var booster=Call(economy,"CreativeAction","dyeBoosterBuy",Input("operationId",Operation(economy)));var dye=((IEnumerable)Get(booster,"Dyes")).Cast<object>().Single();string dyeId=(string)Get(dye,"Id");
        string artwork=(string)Call(economy,"StoreCustomArtwork",Bytes(Artwork));var paid=Input("operationId",Operation(economy),"cardId",id,"dyeId",dyeId,"artworkId",artwork,"artworkScale",1d,"artworkX",0d,"artworkY",0d);
        var artQuote=Call(economy,"CreativeQuote",paid);Check((int)Get(artQuote,"TotalGems")>0&&(bool)Get(artQuote,"DyeConsumed"),"New artwork and dye pay their actual initial cost");
        Call(economy,"CreativeAction","cosmeticSave",paid);int afterPayment=(int)Get(Call(economy,"Snapshot"),"Gems");
        var moved=Input("operationId",Operation(economy),"cardId",id,"artworkScale",2.5d,"artworkX",-1d,"artworkY",1d);var moveQuote=Call(economy,"CreativeQuote",moved);
        Check((int)Get(moveQuote,"TotalGems")==0&&!(bool)Get(moveQuote,"DyeConsumed"),"Moving previously purchased artwork never repays art or consumes dye");
        Call(economy,"CreativeAction","cosmeticSave",moved);Check((int)Get(Call(economy,"Snapshot"),"Gems")==afterPayment,"Movement with exhausted applied dye costs no gems");
        var styled=Call(economy,"GetCardCosmetic",id);Check((string)Get(styled,"DyeId")==dyeId&&(string)Get(styled,"ArtworkId")==artwork,"Implicit movement preserves paid artwork and applied dye");
        Check(((IEnumerable)Get(Call(economy,"CreativeSnapshot"),"Dyes")).Cast<object>().All(d=>(int)Get(d,"Count")==0),"Movement does not consume an additional dye");
        using(var preview=Pixels((string)Get(Preview(economy,id,Input("artworkScale",2.5d,"artworkX",-1d,"artworkY",1d)),"imageData")))using(var applied=Pixels((string)Call(economy,"ReadLifestyleImage",id,false)))
            Check(ImageHash(preview,new Rectangle(0,0,750,1050))==ImageHash(applied,new Rectangle(0,0,750,1050)),"Authenticated preview equals saved positioned appearance");
    }
    private static void RendererBoundariesAndBadges(string outRoot)
    {
        Static(rendererType,"ClearCache");var badgeHashes=new HashSet<string>();
        foreach(string rarity in new[]{"Common","Uncommon","Rare","SR","SSR","UR"})
        {
            var card=Card(rarity,"portrait",rarity=="SSR"||rarity=="UR"?"full-v4":"window-v4");string canonical=Render(card,Png(750,1050,false),null,Artwork,false);
            string centered=Render(card,canonical,Style(card,1,0,0,"",false),Artwork,false);
            string moved=Render(card,canonical,Style(card,3,1,-1,"",false),Artwork,false);
            string recolored=Render(card,canonical,Style(card,1,0,0,"#D72665",false),Artwork,false);
            string mystic=Render(card,canonical,Style(card,1,0,0,"#16D2C1",true),Artwork,false);
            bool low=rarity!="SSR"&&rarity!="UR";int badgeWidth=low?132:150;Rectangle badge=new Rectangle(750-badgeWidth-52,low?(int)(1050*.652f):40,badgeWidth,(int)Math.Ceiling(badgeWidth*64d/180));
            using(var first=Pixels(centered))using(var changed=Pixels(moved))using(var dyed=Pixels(recolored))using(var glowing=Pixels(mystic))
            {
                Check(ImageHash(first,new Rectangle(180,120,240,300))!=ImageHash(changed,new Rectangle(180,120,240,300)),"Movement visibly changes interior artwork: "+rarity);
                Check(ImageHash(first,new Rectangle(0,0,31,1050))==ImageHash(changed,new Rectangle(0,0,31,1050))&&ImageHash(first,new Rectangle(719,0,31,1050))==ImageHash(changed,new Rectangle(719,0,31,1050)),"Movement cannot draw into frame: "+rarity);
                if(low)Check(ImageHash(first,new Rectangle(34,665,682,345))==ImageHash(changed,new Rectangle(34,665,682,345)),"Low-tier clipped artwork cannot spill into character/footer info: "+rarity);
                string badgeHash=ImageHash(first,badge);Check(badgeHash==ImageHash(dyed,badge)&&badgeHash==ImageHash(glowing,badge),"Fixed rarity badge pixels unaffected by regular or Mystic dye: "+rarity);Check(badgeHashes.Add(badgeHash),"Fixed badge has distinct tier art: "+rarity);
                changed.Save(Path.Combine(outRoot,rarity+"-positioned.png"));
            }
            string repeat=Render(card,canonical,Style(card,3,1,-1,"",false),Artwork,false);Check(repeat==moved,"Cached position returns exact matching image: "+rarity);
        }
        var diagnostics=Static(rendererType,"CacheDiagnostics");Check((long)Get(diagnostics,"Hits")>=6&&(int)Get(diagnostics,"Details")<=12&&(int)Get(diagnostics,"Bytes")<=32*1024*1024,"Position cache uses distinct values and bounded native LRU");
    }
    private static long Count(object economy,string field){return (long)economy.GetType().GetField(field,flags).GetValue(economy);}
    private static object ProtectedVault(string root)
    {
        var protector=Activator.CreateInstance(app.GetType("HYROLauncherWeb.HyTcgUserProtector"));
        Type vaultType=economyType.GetNestedType("Vault",BindingFlags.NonPublic);
        return json.Deserialize(Encoding.UTF8.GetString((byte[])Call(protector,"Unprotect",File.ReadAllBytes(Path.Combine(root,"hytcg-vault.dat")))),vaultType);
    }
    private static void AlphaThumbnailAndProtectedDiskPreview(string root)
    {
        var economy=Economy(root);var card=Mint(economy,root,"Common");string id=(string)Get(card,"CardId"),vaultFile=Path.Combine(root,"hytcg-vault.dat"),cardFile=Path.Combine(root,"cards",id+"-r0.hycard");
        string canonical=(string)Call(economy,"ReadCardImage",id),vaultBefore=HashFile(vaultFile),cardBefore=HashFile(cardFile);
        string native=(string)Static(storageType,"Thumbnail",Bytes(canonical));
        Check(native.StartsWith("data:image/png;base64,",StringComparison.Ordinal),"Native thumbnails encode PNG for alpha");
        using(var image=Pixels(native))
        {Check(image.GetPixel(0,0).A==0,"Native rounded thumbnail corner stays transparent");Check(image.GetPixel(image.Width/2,image.Height/3).A==255,"Native thumbnail art stays opaque");}
        string preview=(string)Call(economy,"ReadLifestyleImage",id,true);string previewFile=Directory.GetFiles(Path.Combine(root,"hytcg-previews"),"*.hypreview").Single();byte[] authenticatedPreview=File.ReadAllBytes(previewFile);
        Check(Encoding.ASCII.GetString(authenticatedPreview,0,15)=="HYTCG-PREVIEW2\0","Protected disk cache uses alpha PNG preview version");
        Check(!(authenticatedPreview[0]==137&&authenticatedPreview[1]==80),"Disk preview is a protected container, not a directly viewable PNG");
        Check(Count(economy,"previewGenerations")==1,"First native preview generates one protected cache entry");
        economy=Economy(root);string loaded=(string)Call(economy,"ReadLifestyleImage",id,true);
        Check(preview==loaded&&Count(economy,"previewDiskHits")==1&&Count(economy,"previewGenerations")==0,"Reload reads identical authenticated preview PNG from disk");
        using(var image=Pixels(loaded))Check(image.GetPixel(0,0).A==0&&image.GetPixel(image.Width/2,image.Height/3).A==255,"Reloaded disk PNG preserves transparent corner and opaque artwork");
        Check(HashFile(vaultFile)==vaultBefore&&HashFile(cardFile)==cardBefore,"Preview generation/disk load does not mutate signed account/card bytes");
        byte[] corrupted=(byte[])authenticatedPreview.Clone();corrupted[corrupted.Length-1]^=1;File.WriteAllBytes(previewFile,corrupted);
        economy=Economy(root);string repaired=(string)Call(economy,"ReadLifestyleImage",id,true);
        Check(repaired==preview&&Count(economy,"previewDiskHits")==0&&Count(economy,"previewGenerations")==1,"Tampered preview HMAC rejects disk pixels and regenerates from authenticated card");
        string otherRoot=Path.Combine(root,"other-account");var other=Economy(otherRoot);var otherCard=Mint(other,otherRoot,"Common");string otherId=(string)Get(otherCard,"CardId");
        var otherOwned=((IEnumerable)Get(ProtectedVault(otherRoot),"Cards")).Cast<object>().Single();string otherHash=(string)Get(otherOwned,"PngHash");Directory.CreateDirectory(Path.Combine(otherRoot,"hytcg-previews"));
        File.WriteAllBytes(Path.Combine(otherRoot,"hytcg-previews",otherHash+".hypreview"),authenticatedPreview);string otherPreview=(string)Call(other,"ReadLifestyleImage",otherId,true);
        Check(otherPreview.StartsWith("data:image/png;base64,")&&Count(other,"previewDiskHits")==0&&Count(other,"previewGenerations")==1,"Preview replay across account/hash is rejected and regenerated locally");
        // Even warm memory/disk previews cannot bypass current owned container checks.
        byte[] badCard=File.ReadAllBytes(cardFile);badCard[badCard.Length-1]^=1;File.WriteAllBytes(cardFile,badCard);
        Fails("CardInvalid",delegate{Call(economy,"ReadCardImage",id);});Fails("CardInvalid",delegate{Call(economy,"ReadLifestyleImage",id,false);});
        Fails("CardUnavailable",delegate{Call(economy,"ReadLifestyleImage",id,true);});
        Check(HashFile(vaultFile)==vaultBefore,"Rejected authenticated reads do not rewrite account progress");
    }
    private static int Main(string[] args)
    {
        string root=args[1];Directory.CreateDirectory(root);
        try
        {
            app=Assembly.LoadFrom(args[0]);economyType=app.GetType("HYROLauncherWeb.HyTcgEconomy");cardType=app.GetType("HYROLauncherWeb.HyTcgCard");styleType=app.GetType("HYROLauncherWeb.HyTcgCosmetic");rendererType=app.GetType("HYROLauncherWeb.HyTcgCosmeticRenderer");storageType=app.GetType("HYROLauncherWeb.HyTcgStorage");pngType=app.GetType("HYROLauncherWeb.HyTcgService+Png");
            NewPortraitAndUpgrade(Path.Combine(root,"issuance"));LegacyLandscape(Path.Combine(root,"legacy"));PositionContract(Path.Combine(root,"position"));RendererBoundariesAndBadges(root);AlphaThumbnailAndProtectedDiskPreview(Path.Combine(root,"alpha-preview"));
            File.WriteAllText(Path.Combine(root,"report.json"),json.Serialize(new {Status=softFailures.Count==0?"PASS":"FAIL",Checks=checks,Failures=softFailures,Fixture=root,Candidate=args[0]}));
            if(softFailures.Count>0)throw new Exception(String.Join("; ",softFailures));Console.WriteLine("PASS "+checks+" native portrait/art position checks");return 0;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(root,"report.json"),json.Serialize(new {Status="FAIL",Checks=checks,Error=e.ToString(),SoftFailures=softFailures,Fixture=root,Candidate=args[0]}));Console.Error.WriteLine(e);return 1;}
    }
}
