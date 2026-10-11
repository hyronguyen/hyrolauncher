using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

namespace HYROLauncherWeb
{
    internal partial class WebLauncherForm
    {
        // Unused movie/launcher host hooks only; all TCG dispatch/image handlers
        // compile unchanged from the real native sources for this verifier.
        private object tcgMissionCache;
        private long tcgMovieUncommittedMs,tcgMovieLastMs;
        private readonly System.Diagnostics.Stopwatch tcgMovieClock=System.Diagnostics.Stopwatch.StartNew();
        private const string BuiltInMoviePluginId="builtin.movie";
        private void TcgStartMovieMonitor(){}
        private void TcgMissionPlay(string id){}
        private void ShowBuiltInPlugin(string id){}
    }
}

// No production account is opened. The actual native HandleHyTcgMessage dispatches
// every request; only browser document identity and reply collection use host stubs.
internal static class VerifyHyTcgLifestyleBridge
{
    private static int checks,sequence;
    private static readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=64*1024*1024};
    private static readonly BindingFlags instance=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly MethodInfo dispatch=typeof(WebLauncherForm).GetMethod("HandleHyTcgMessage",instance);
    private sealed class OfflineTransport : IHyTcgTransport
    {
        internal int Calls;
        internal string FixtureImage;
        public string Json(string url,string userAgent,int timeout,CancellationToken token) {Interlocked.Increment(ref Calls);throw new Exception("Unexpected external fixture request");}
        public string Image(string url,string userAgent,int timeout,CancellationToken token) {Interlocked.Increment(ref Calls);if(FixtureImage!=null)return FixtureImage;throw new Exception("Unexpected external fixture image request");}
    }
    private sealed class Fixture
    {
        internal DateTime Now=new DateTime(2026,10,10,0,0,0,DateTimeKind.Utc);
        internal readonly string Root;
        internal readonly OfflineTransport Transport=new OfflineTransport();
        internal readonly HyTcgEconomy Economy;
        internal readonly HyTcgService Service;
        internal readonly WebLauncherForm Host;
        internal readonly PluginBrowserWindow Window;
        internal readonly List<HyTcgCard> Cards=new List<HyTcgCard>();
        internal readonly Dictionary<string,string> Files=new Dictionary<string,string>();
        internal Fixture(string root,int count)
        {
            Root=root;Directory.CreateDirectory(root);
            Economy=new HyTcgEconomy(root,delegate {return Now;},new HyTcgUserProtector(),delegate {return TimeSpan.Zero;});
            Service=new HyTcgService(root,Transport,Economy);Host=new WebLauncherForm(Service);
            Window=(PluginBrowserWindow)Field(Host,"pluginWindow");
            string png=Png(750,1050,Color.MidnightBlue);
            for(int i=0;i<count;i++)
            {
                var card=new HyTcgCard {CardId="HT-20261010-"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant(),CharacterId=177+Math.Min(i,3),
                    Name="Bridge companion "+i,Series="Fixture series",Gender="Female",Rarity=i<3?"UR":i==3?"Common":"Uncommon",
                    CreatedAt=Now.AddSeconds(-i).ToString("o"),Orientation="portrait",ImageSource="acdb",
                    SourceImage="https://ami.animecharactersdatabase.com/fixture.jpg",ProfileImage="https://ami.animecharactersdatabase.com/fixture.jpg"};
                card.RarityColor=HyTcgRules.RarityColor(card.Rarity);var pull=Economy.Reserve(1);string token=pull.Tokens[0];
                Economy.Begin(token);Economy.AuthorizeCard(token,card);Files.Add(card.CardId,Service.SaveCard(card,png,token));Cards.Add(card);
            }
        }
        internal Dictionary<string,object> Rpc(string action,params object[] fields) {var reply=Request("hytcgLifestyle",action,fields);if(action=="fight" && reply.ContainsKey("gameplay") && (!reply.ContainsKey("error") || Convert.ToString(reply["error"])==""))for(int i=0;i<60 && !State(reply).Battle.Complete;i++){var b=State(reply).Battle;reply=Request("hytcgLifestyle","battleTurn",new object[]{"battleId",b.Id,"turn",b.Turn,"skillId","strike"});}return reply;}
        internal Dictionary<string,object> Image(params object[] fields) {return Request("hytcgLifestyleImage","",fields);}
        internal Dictionary<string,object> Native(string type,string responseType,params object[] fields)
        {
            string request="rpc-"+Interlocked.Increment(ref sequence);var input=Envelope(type,"",request);
            for(int i=0;i<fields.Length;i+=2)input[(string)fields[i]]=fields[i+1];Send(Host,Window,input);
            var reply=Window.WaitFor(request,responseType);
            Check(Convert.ToString(reply["type"])==responseType && Convert.ToString(reply["requestId"])==request && Convert.ToString(reply["session"])=="fixture-session","Actual native dispatcher response: "+type);
            return reply;
        }
        private Dictionary<string,object> Request(string type,string action,object[] fields)
        {
            string request="rpc-"+Interlocked.Increment(ref sequence);var input=Envelope(type,action,request);
            for(int i=0;i<fields.Length;i+=2)input.Add((string)fields[i],fields[i+1]);
            if((action=="cosmeticSave" || action=="dyeBoosterBuy" || action=="frameCreate" || action=="albumCreate" || action=="albumSlots" || action=="albumRestyle") && !input.ContainsKey("operationId"))
            {object creative=Economy.CreativeSnapshot();long next=Convert.ToInt64(creative.GetType().GetProperty("NextStudioSequence").GetValue(creative,null));input["operationId"]="SC-"+next+"-"+Guid.NewGuid().ToString("N");}
            Send(Host,Window,input);
            var reply=Window.WaitFor(request,type);Check(Convert.ToString(reply["type"])==type && Convert.ToString(reply["requestId"])==request && Convert.ToString(reply["session"])=="fixture-session","RPC response envelope: "+action);
            Check(((SemaphoreSlim)Field(Host,"hyTcgLifestyleGate")).CurrentCount==2 && ((HashSet<string>)Field(Host,"hyTcgLifestyleRequests")).Count==0,"Completed RPC releases native gate and request: "+action);
            return reply;
        }
    }
    private static object Field(object target,string name) {return target.GetType().GetField(name,instance).GetValue(target);}
    private static void Check(bool pass,string name) {checks++;if(!pass)throw new Exception(name);}
    private static Dictionary<string,object> Envelope(string type,string action,string request)
    {return new Dictionary<string,object>{{"type",type},{"session","fixture-session"},{"requestId",request},{"action",action}};}
    private static void Send(WebLauncherForm host,PluginBrowserWindow window,Dictionary<string,object> input)
    {dispatch.Invoke(host,new object[]{json.Deserialize<Dictionary<string,object>>(json.Serialize(input)),window});}
    private static Dictionary<string,object> Success(Dictionary<string,object> reply)
    {Check(Convert.ToString(reply["error"])=="","Expected successful bridge reply "+Convert.ToString(reply["type"])+"; received "+Convert.ToString(reply["error"]));return reply;}
    private static void Error(string code,Dictionary<string,object> reply)
    {Check(Convert.ToString(reply["error"])==code,"Expected bridge error "+code+"; received "+Convert.ToString(reply["error"]));Check(!reply.ContainsKey("result") && !reply.ContainsKey("imageData"),"Failed bridge action contains no successful outcome/image");}
    private static HyTcgGameplayPage State(Dictionary<string,object> reply) {return (HyTcgGameplayPage)reply["gameplay"];}
    private static HyTcgGameplayOutcome Outcome(Dictionary<string,object> reply) {return (HyTcgGameplayOutcome)reply["result"];}
    private static HyTcgWallet Wallet(Dictionary<string,object> reply) {return (HyTcgWallet)reply["wallet"];}
    private static List<HyTcgAlbum> Albums(Dictionary<string,object> reply)
    {object creative=reply["creative"];return (List<HyTcgAlbum>)creative.GetType().GetProperty("Albums").GetValue(creative,null);}
    private static int CosmeticCount(Dictionary<string,object> reply)
    {object creative=reply["creative"];return ((IEnumerable)creative.GetType().GetProperty("Cosmetics").GetValue(creative,null)).Cast<object>().Count();}
    private static T Creative<T>(Dictionary<string,object> reply,string name)
    {object creative=reply["creative"];return (T)creative.GetType().GetProperty(name).GetValue(creative,null);}
    private static void ProtectedFixture(Fixture fixture,Action<Dictionary<string,object>> change)
    {
        string file=Path.Combine(fixture.Root,"hytcg-vault.dat");var protector=new HyTcgUserProtector();
        var vault=json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(file))));change(vault);
        File.WriteAllBytes(file,protector.Protect(Encoding.UTF8.GetBytes(json.Serialize(vault))));
    }
    private static Dictionary<string,object> PurchaseAlbum(Fixture f,string name,string background,string[] cards)
    {ProtectedFixture(f,v=>v["Gems"]=Convert.ToInt32(v["Gems"])+2000);var created=Success(f.Rpc("albumCreate","name",name,"background",background,"cover",background));return Success(f.Rpc("albumSave","albumId",Albums(created).Last().Id,"name",name,"cardIds",cards));}
    private static void SignedFixtureCard(Fixture fixture,string cardId,Action<HyTcgCard> change)
    {
        // Fixture preparation models an existing pre-window-v1 signed card. The
        // test's account key/signature helper never touches a production directory.
        if(fixture.Root.IndexOf("hytcg-lifestyle-bridge-",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Fixture-only card signature setup required");
        string file=Path.Combine(fixture.Root,"hytcg-vault.dat");var protector=new HyTcgUserProtector();
        Type vaultType=typeof(HyTcgEconomy).GetNestedType("Vault",BindingFlags.NonPublic);
        object vault=json.Deserialize(Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(file))),vaultType);
        var cards=(IList)vaultType.GetProperty("Cards").GetValue(vault,null);
        for(int i=0;i<cards.Count;i++)
        {
            object owned=cards[i];Type ownedType=owned.GetType();if((string)ownedType.GetProperty("CardId").GetValue(owned,null)!=cardId)continue;
            var card=(HyTcgCard)ownedType.GetProperty("Card").GetValue(owned,null);change(card);
            string filename=(string)ownedType.GetProperty("FileName").GetValue(owned,null);
            cards[i]=typeof(HyTcgEconomy).GetMethod("CreateOwned",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{vault,card,filename,File.ReadAllBytes(fixture.Files[cardId]),false});
            File.WriteAllBytes(file,protector.Protect(Encoding.UTF8.GetBytes(json.Serialize(vault))));return;
        }
        throw new Exception("Fixture card missing");
    }
    private static string Png(int width,int height,Color color)
    {
        using(var bitmap=new Bitmap(width,height))using(var output=new MemoryStream())
        {using(var g=Graphics.FromImage(bitmap)){g.Clear(color);g.FillRectangle(Brushes.Gold,width/3,height/3,width/3,height/3);}bitmap.Save(output,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(output.ToArray());}
    }
    private static Bitmap BitmapOf(string data)
    {using(var stream=new MemoryStream(Convert.FromBase64String(data.Substring(data.IndexOf(',')+1))))using(var source=new Bitmap(stream))return new Bitmap(source);}
    private static string HashFile(string path)
    {using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));}
    private static string ProtectedStateHash(string path)
    {using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(new HyTcgUserProtector().Unprotect(File.ReadAllBytes(path))));}
    private static List<Dictionary<string,object>> Replies(PluginBrowserWindow window)
    {return (List<Dictionary<string,object>>)Field(window,"replies");}
    private static int ReplyCount(PluginBrowserWindow window)
    {var list=Replies(window);lock(list)return list.Count;}
    private static void Ignored(Fixture f,Dictionary<string,object> input,PluginBrowserWindow window,string name)
    {int count=ReplyCount(f.Window),gems=f.Economy.Snapshot().Gems;Send(f.Host,window,input);Check(ReplyCount(f.Window)==count && f.Economy.Snapshot().Gems==gems && ((HashSet<string>)Field(f.Host,"hyTcgLifestyleRequests")).Count==0,name);}

    private static void BasicAndCreative(Fixture f)
    {
        string id=f.Cards[0].CardId;var snapshot=Success(f.Rpc("snapshot"));
        Check(Wallet(snapshot).Gems==1800 && Wallet(snapshot).FreeCredits==0,"Snapshot preserves paid balance and native ownership credit consumption");
        Check(State(snapshot).Profiles.Count==5 && State(snapshot).Routes.Count==3 && State(snapshot).Inventory.Single(i=>i.Id=="training").Count==3,"Actual snapshot supplies gameplay defaults and owned profiles");
        Check(Albums(snapshot).Count==0 && CosmeticCount(snapshot)==0 && (int)snapshot["rosterTotal"]==5,"Creative and roster snapshot defaults");
        Check(Creative<List<string>>(snapshot,"OwnedFrameIds").SequenceEqual(new[]{"classic"}) && Creative<List<HyTcgFrameDye>>(snapshot,"Dyes").Count==0,"New account starts with only Classic and no free dyes");
        Check(!json.Serialize(snapshot).Contains("data:image/") && !json.Serialize(snapshot).Contains(f.Root) && !json.Serialize(snapshot).Contains("ContainerHash"),"Snapshot contains metadata, no eager image/native file authority");
        var roster=Success(f.Rpc("roster","query","Bridge companion","offset",1,"count",2));
        var cards=((IEnumerable<HyTcgLifestyleCard>)roster["roster"]).ToList();
        Check(cards.Count==2 && (int)roster["rosterTotal"]==5 && (int)roster["nextOffset"]==3 && (bool)roster["hasMore"],"Roster search and pagination traverses native metadata");
        Check(((HyTcgGameplayRosterPage)roster["profilePage"]).Offset==1 && ((IEnumerable<HyTcgGameplayProfile>)roster["profiles"]).Count()==4,"Roster response carries the bounded profile page");
        var bounded=Success(f.Rpc("roster","offset",9999999,"count",9999999));Check(((IEnumerable<HyTcgLifestyleCard>)bounded["roster"]).Count()==0 && (int)bounded["nextOffset"]==5,"RPC clamps roster count and offset");
        var selected=Success(f.Rpc("profile","cardId",id));Check(((HyTcgGameplayProfile)selected["profile"]).CardId==id && ((HyTcgLifestyleCard)selected["card"]).CardId==id,"Selected profile returns matching native card metadata");
        var baseline=Success(f.Image("cardId",id,"original",true));string original=(string)baseline["imageData"];
        Check(original==f.Economy.ReadCardImage(id),"Original image RPC returns authenticated canonical bytes");
        Check((string)Success(f.Image("cardId",id))["imageData"]==original,"Unstyled detail uses canonical image");
        using(var thumbnail=BitmapOf((string)Success(f.Image("cardId",id,"thumbnail",true))["imageData"]))Check(thumbnail.Width<750 && thumbnail.Height<1050 && thumbnail.Width>0,"Image thumbnail is bounded and decodable");
        var album=PurchaseAlbum(f,"Bridge album","sakura",new[]{id,"",f.Cards[1].CardId});string albumId=Albums(album).Single().Id;
        Check(Albums(album).Single().CardIds.SequenceEqual(new[]{id,"",f.Cards[1].CardId,""}) && Wallet(album).Gems==1800,"Paid album preserves four sparse native card slots");
        var edited=Success(f.Rpc("albumSave","albumId",albumId,"name","Edited album","cardIds",new[]{f.Cards[2].CardId}));Check(Albums(edited).Single().Id==albumId && Albums(edited).Single().Name=="Edited album","Existing album is updated by native ID");
        Error("AlbumInvalid",f.Rpc("albumSave","name","Duplicate","cardIds",new[]{id,id}));
        Check(Albums(Success(f.Rpc("snapshot"))).Single().Name=="Edited album","Malformed album request cannot mutate prior album");
        var drawn=Success(f.Rpc("artworkDraw","png",Png(64,48,Color.HotPink)));string artwork=(string)drawn["artworkId"];
        using(var bitmap=BitmapOf((string)drawn["imageData"]))Check(artwork.Length==64 && bitmap.Width==64 && bitmap.Height==48,"Drawing RPC normalizes and stores native artwork");
        Check((string)Success(f.Rpc("artworkDraw","png",Png(64,48,Color.HotPink)))["artworkId"]==artwork,"Repeated drawing deduplicates native content");
        Error("ArtworkInvalid",f.Rpc("artworkDraw","png","data:image/png;base64,bm90LWFuLWltYWdl"));
        Error("ArtworkUrlInvalid",f.Rpc("artworkUrl","url","https://127.0.0.1/private.png"));
        Error("DyeInvalid",f.Rpc("cosmeticSave","cardId",id,"frame","classic","dye","#40abcd","artworkId",artwork));
        Error("FrameRetired",f.Rpc("cosmeticSave","cardId",id,"frame","petal"));
        var bought=Success(f.Rpc("dyeBoosterBuy","gems",0,"mystic",true,"color","#000000"));var receipt=Creative<HyTcgStudioReceipt>(bought,"LastStudioReceipt");
        var dye=Creative<List<HyTcgFrameDye>>(bought,"Dyes").Single();string boosterOperation=receipt.OperationId;
        Check(Wallet(bought).Gems==1750 && dye.Count==1 && receipt.GemsSpent==50 && dye.Id==receipt.DyeId,"Dye booster resolves native color/variant and charges exactly50 gems");
        var replay=Success(f.Rpc("dyeBoosterBuy","operationId",boosterOperation));Check(Wallet(replay).Gems==1750 && Creative<List<HyTcgFrameDye>>(replay,"Dyes").Single().Count==1,"Replayed booster operation cannot reroll or charge twice");
        Error("StudioReplayMismatch",f.Rpc("cosmeticSave","operationId",boosterOperation,"cardId",id,"frame","classic"));
        Error("StudioOperationInvalid",f.Rpc("dyeBoosterBuy","operationId","bad-operation"));
        string beforeQuote=HashFile(Path.Combine(f.Root,"hytcg-vault.dat"));var quote=(HyTcgStudioQuote)Success(f.Rpc("quote","cardId",id,"frame","classic","dyeId",dye.Id,"artworkId",artwork,"pixels",0,"gems",0))["quote"];
        Check(quote.ArtworkPixels==64*48 && quote.TotalGems==1 && quote.DyeConsumed && HashFile(Path.Combine(f.Root,"hytcg-vault.dat"))==beforeQuote,"Authoritative read-only quote counts visible artwork pixels and rejects renderer-supplied price");
        var styled=Success(f.Rpc("cosmeticSave","cardId",id,"frame","classic","dyeId",dye.Id,"artworkId",artwork));string appliedOperation=Creative<HyTcgStudioReceipt>(styled,"LastStudioReceipt").OperationId;
        Check(CosmeticCount(styled)==1 && Wallet(styled).Gems==1749 && Creative<List<HyTcgFrameDye>>(styled,"Dyes").Count==0 && !json.Serialize(styled["creative"]).Contains("data:image/"),"Cosmetic apply consumes one purchased dye and exactly one gem for3072 visible pixels");
        var repeat=Success(f.Rpc("cosmeticSave","operationId",appliedOperation,"cardId",id,"frame","classic","dyeId",dye.Id,"artworkId",artwork));
        Check(Wallet(repeat).Gems==1749 && Creative<HyTcgStudioReceipt>(repeat,"LastStudioReceipt").OperationId==appliedOperation,"Apply receipt replay never consumes or charges again");
        string composite=(string)Success(f.Image("cardId",id))["imageData"];
        using(var bitmap=BitmapOf(composite))Check(bitmap.Width==750 && bitmap.Height==1050 && composite!=original,"Native cosmetic RPC returns a full composited card");
        string previewVault=HashFile(Path.Combine(f.Root,"hytcg-vault.dat"));
        var appliedPreview=Success(f.Image("cardId",id,"previewFrameId","classic","previewDyeId",dye.Id));
        var previewStyle=(HyTcgCosmetic)appliedPreview["cosmetic"];
        Check((string)appliedPreview["imageData"]==composite && previewStyle.DyeId==dye.Id && previewStyle.Dye==dye.Color && previewStyle.Mystic==dye.Mystic && previewStyle.ArtworkId==artwork,"Preview may reuse the current applied dye after its purchased quantity is consumed");
        Check(HashFile(Path.Combine(f.Root,"hytcg-vault.dat"))==previewVault,"Applied preview does not persist or charge protected state");
        using(var bitmap=BitmapOf((string)Success(f.Image("cardId",id,"thumbnail",true))["imageData"]))Check(bitmap.Width<750 && bitmap.Height<1050,"Styled thumbnail remains bounded");
        Check((string)Success(f.Image("cardId",id,"original",true))["imageData"]==original,"Original branch bypasses display cosmetics without changing canonical bytes");
        using(var bitmap=BitmapOf((string)Success(f.Image("cardId",id,"artworkId",artwork))["imageData"]))Check(bitmap.Width==64 && bitmap.Height==48,"Artwork image RPC reads normalized owned artwork");
        Error("FrameRetired",f.Rpc("cosmeticSave","cardId",id,"frame","forged"));
        Check(CosmeticCount(Success(f.Rpc("snapshot")))==1,"Rejected cosmetic leaves prior style intact");
        var reset=Success(f.Rpc("cosmeticReset","cardId",id));Check(CosmeticCount(reset)==0 && (string)Success(f.Image("cardId",id))["imageData"]==original,"Cosmetic reset restores canonical display");
        var restored=Success(f.Rpc("cosmeticSave","cardId",id,"restorePrevious",true,"frame","classic","dyeId",dye.Id,"artworkId",artwork));
        Check(Wallet(restored).Gems==1749 && CosmeticCount(restored)==1,"Restoring the exact previously paid look does not consume a second dye or artwork fee");
        Success(f.Rpc("cosmeticReset","cardId",id));
        Check(Albums(Success(f.Rpc("albumDelete","albumId",albumId))).Count==0,"Album delete removes native album");
        Error("AlbumUnavailable",f.Rpc("albumDelete","albumId",albumId));
    }
    private static void StudioCreationAndPreview(Fixture f,Fixture foreign)
    {
        string id=f.Cards[0].CardId,vaultFile=Path.Combine(f.Root,"hytcg-vault.dat"),container=HashFile(f.Files[id]);
        var initial=Success(f.Rpc("snapshot"));int starting=Wallet(initial).Gems;string before=ProtectedStateHash(vaultFile);
        Check(Creative<string>(initial,"FrameMode")=="rarity" && Creative<List<HyTcgFrameDefinition>>(initial,"FrameCatalog").Count==0 && Creative<List<HyTcgFrameItem>>(initial,"FrameItems").Count==0,"Snapshot exposes automatic rarity frames with no custom-frame inventory or catalog");
        Check(!Creative<List<HyTcgInventoryEntry>>(initial,"InventoryItems").Any(i=>i.Kind=="frame") && Creative<List<HyTcgFrameMaterial>>(initial,"FrameCreationMaterials").Count==0,"Unified inventory and recipe do not expose retired frames");
        Error("FrameRetired",f.Rpc("frameBuy","frameId","moonlace")); Check(ProtectedStateHash(vaultFile)==before,"Retired frame rejected request changed protected progress");
        Error("FrameRetired",f.Rpc("frameCreate","frameId","moonlace","name","Disabled creation")); Check(ProtectedStateHash(vaultFile)==before,"Retired frame rejected request changed protected progress");
        Error("FrameRetired",f.Rpc("frameCreate","operationId","invalid","frameId","classic")); Check(ProtectedStateHash(vaultFile)==before,"Retired frame rejected request changed protected progress");
        Error("FrameRetired",f.Rpc("cosmeticSave","cardId",id,"frame","moonlace")); Check(ProtectedStateHash(vaultFile)==before,"Retired frame rejected request changed protected progress");
        Error("FrameRetired",f.Rpc("quote","cardId",id,"frameItemId","FI-"+new string('a',32))); Check(ProtectedStateHash(vaultFile)==before,"Retired frame rejected request changed protected progress");
        Error("FrameRetired",f.Image("cardId",id,"previewFrameId","moonlace")); Check(ProtectedStateHash(vaultFile)==before,"Retired frame rejected request changed protected progress");
        Error("FrameRetired",f.Image("cardId",id,"previewFrameItemId","FI-"+new string('a',32))); Check(ProtectedStateHash(vaultFile)==before,"Retired frame rejected request changed protected progress");
        Error("FrameRetired",f.Image("cardId",id,"previewFrameDraft",new Dictionary<string,object>{{"frameId","moonlace"}})); Check(ProtectedStateHash(vaultFile)==before,"Retired frame rejected request changed protected progress");
        Check(ProtectedStateHash(vaultFile)==before,"Retired frame actions changed protected progress");
        Check(HashFile(f.Files[id])==container && f.Economy.Snapshot().Gems==starting,"Retired frame actions cannot alter currency or protected card");
        Error("CardAlreadyDissolved",f.Image("cardId",foreign.Cards[0].CardId,"previewFrameId","classic"));
        var booster=Success(f.Rpc("dyeBoosterBuy"));var dye=Creative<List<HyTcgFrameDye>>(booster,"Dyes").Single();
        var drawn=Success(f.Rpc("artworkDraw","png",Png(80,60,Color.Teal)));string artwork=(string)drawn["artworkId"];
        before=HashFile(vaultFile);
        var preview=Success(f.Image("cardId",id,"previewFrameId","classic","previewDyeId",dye.Id,"previewArtworkId",artwork));
        var quote=(HyTcgStudioQuote)Success(f.Rpc("quote","cardId",id,"dyeId",dye.Id,"artworkId",artwork))["quote"];
        Check(quote.DyeConsumed && !quote.FrameItemConsumed && quote.ArtworkPixels==4800 && quote.TotalGems==1 && HashFile(vaultFile)==before,"Rarity frame preview and artwork quote preserve authoritative dye and pixel charges");
        var saved=Success(f.Rpc("cosmeticSave","cardId",id,"dyeId",dye.Id,"artworkId",artwork));
        Check(Wallet(saved).Gems==starting-51 && f.Economy.GetCardCosmetic(id).Frame=="classic" && f.Economy.GetCardCosmetic(id).FrameItemId=="","Dye and artwork apply without creating or consuming frame items");
        Check((string)Success(f.Image("cardId",id))["imageData"]==(string)preview["imageData"],"Applied rarity frame matches native Studio preview");
        Check(HashFile(f.Files[id])==container && f.Transport.Calls==0,"Retirement and decoration preserve canonical card and stay offline");
    }
    private static void LegacyStudio(Fixture f)
    {
        string id=f.Cards[0].CardId,container=HashFile(f.Files[id]);
        var old=new HyTcgCosmetic {CardId=id,Frame="petal",Dye="#AABBCC",DyeId=null,Mystic=false,Morph="#445566",MorphMode="multiply",ArtworkId=""};
        ProtectedFixture(f,delegate(Dictionary<string,object> vault) {vault["Creative"]=new HyTcgCreativeState {Albums=new List<HyTcgAlbum>(),Cosmetics=new List<HyTcgCosmetic>{old},Artworks=new List<HyTcgCustomArtwork>(),ResetArtworkIds=new List<string>()};});
        var migrated=Success(f.Rpc("snapshot"));int gems=Wallet(migrated).Gems;
        Check(Creative<List<string>>(migrated,"OwnedFrameIds").SequenceEqual(new[]{"classic"}) && Creative<List<HyTcgFrameDye>>(migrated,"Dyes").Count==0 && CosmeticCount(migrated)==1 && gems==1800,"Protected legacy applied frame is grandfathered without retrospective currency or free dye grants");
        var look=f.Economy.GetCardCosmetic(id);
        Check(look.DyeId==null && look.Dye==old.Dye && look.Morph==old.Morph && look.MorphMode==old.MorphMode && !look.Mystic,"Legacy migration preserves exact old dye/morph metadata");
        string rendered=(string)Success(f.Image("cardId",id))["imageData"],before=HashFile(Path.Combine(f.Root,"hytcg-vault.dat"));
        var preview=Success(f.Image("cardId",id,"previewFrameId","classic"));
        Check((string)preview["imageData"]==rendered && ((HyTcgCosmetic)preview["cosmetic"]).DyeId==null && HashFile(Path.Combine(f.Root,"hytcg-vault.dat"))==before,"Read-only preview preserves the exact grandfathered legacy look");
        Success(f.Rpc("cosmeticReset","cardId",id));
        var restored=Success(f.Rpc("cosmeticSave","cardId",id,"frame","classic","restorePrevious",true,"dye","#AABBCC","morph","#445566","morphMode","multiply"));
        Check(Wallet(restored).Gems==gems && Creative<HyTcgStudioReceipt>(restored,"LastStudioReceipt").GemsSpent==0 && (string)Success(f.Image("cardId",id))["imageData"]==rendered,"Legacy reset and exact restoration keep paid/grandfathered look without a new fee");
        Error("DyeInvalid",f.Rpc("cosmeticSave","cardId",id,"frame","classic","dye","#000000"));
        Check(f.Economy.Snapshot().Gems==gems && HashFile(f.Files[id])==container && f.Transport.Calls==0,"Forged legacy recolor cannot change canonical card, wallet or native look");
    }
    private static void Gameplay(Fixture f)
    {
        int starting=f.Economy.Snapshot().Gems;string id=f.Cards[0].CardId;var trained=Success(f.Rpc("inventoryUse","itemId","training","cardId",id,"xp",999999));
        Check(Outcome(trained).XpAwarded==80 && State(trained).Profiles.Single(p=>p.CardId==id).Level==2 && State(trained).Inventory.Single(i=>i.Id=="training").Count==2,"Inventory RPC awards native XP and consumes exactly one item");
        var chat=Success(f.Rpc("date","cardId",id,"activity","chat","bond",1000));Check(Outcome(chat).BondAwarded>=12 && Outcome(chat).BondAwarded<=18 && Wallet(chat).Gems==starting,"Date RPC ignores forged bond and returns bounded native affinity");
        var tea=Success(f.Rpc("date","cardId",id,"activity","tea","itemId","tea"));Check(State(tea).Inventory.Single(i=>i.Id=="tea").Count==2 && State(tea).Profiles.Single(p=>p.CardId==id).DatesRemaining==0,"Tea date consumes inventory and per-card quota atomically");
        Error("DateDailyLimit",f.Rpc("date","cardId",id,"activity","chat"));
        var trip=Success(f.Rpc("expeditionStart","routeId","meadow","cardIds",new[]{id},"rewardGems",999999));string expedition=Outcome(trip).ExpeditionId;int reward=State(trip).Expeditions.Single(e=>e.Id==expedition).RewardGems;
        Check(reward>=8 && reward<=12 && State(trip).Profiles.Single(p=>p.CardId==id).Busy && State(trip).ExpeditionStartsRemaining==5,"Expedition native team reward, lock and daily quota");
        Check(((HyTcgGameplayProfile)Success(f.Rpc("profile","cardId",id))["profile"]).Busy,"Busy selected profile remains readable");
        Check(((IEnumerable<HyTcgLifestyleCard>)Success(f.Rpc("roster","query",id))["roster"]).Single().Busy,"Roster exposes expedition lock");
        Success(f.Image("cardId",id,"thumbnail",true));
        Error("CardLocked",f.Rpc("inventoryUse","cardId",id,"itemId","training"));
        Error("CardLocked",f.Rpc("fight","stage",1,"cardIds",new[]{id}));
        Error("CardLocked",f.Rpc("cosmeticSave","cardId",id,"frame","classic"));
        var busyAlbum=PurchaseAlbum(f,"Expedition display","paper",new[]{id});Check(Albums(busyAlbum).Single().CardIds.First()==id,"Album arrangement safely permits expedition busy cards");
        Error("ExpeditionNotReady",f.Rpc("expeditionClaim","expeditionId",expedition));
        Check(f.Economy.Snapshot().Gems==starting,"Early or locked requests cannot award gems");
        f.Now=f.Now.AddSeconds(300);var ready=Success(f.Rpc("snapshot"));Check(State(ready).Expeditions.Single(e=>e.Id==expedition).Ready,"Snapshot uses native clock for claim readiness");
        var claim=Success(f.Rpc("expeditionClaim","expeditionId",expedition));Check(Outcome(claim).GemsAwarded==reward && Wallet(claim).Gems==starting+reward+30 && !State(claim).Profiles.Single(p=>p.CardId==id).Busy,"Claim RPC pays exact native reward and unlocks companion");
        Error("ExpeditionClaimed",f.Rpc("expeditionClaim","expeditionId",expedition));Check(f.Economy.Snapshot().Gems==starting+reward+30,"Repeated RPC claim never awards twice");
        var team=f.Cards.Take(3).Select(c=>c.CardId).ToArray();
        Error("TeamInvalid",f.Rpc("fight","stage",1,"cardIds",new[]{id,id}));Error("FightStageLocked",f.Rpc("fight","stage",20,"cardIds",team));
        Error("GameplayInvalid",f.Rpc("fight","stage",1.5,"cardIds",team));
        var fight=Success(f.Rpc("fight","stage",1,"cardIds",team,"win",false,"reward",999999));
        Check(Outcome(fight).Win && Outcome(fight).GemsAwarded==9 && Outcome(fight).XpAwarded==66 && Outcome(fight).CombatLog.Count>0 && Outcome(fight).Rounds<=20,"Actual fight RPC computes native combat, rewards and log");
        Check(State(fight).Fight.Stage==2 && State(fight).Fight.DailyRemaining==2 && Wallet(fight).Gems==starting+reward+9+60,"Fight progression and wallet match native result");
        Success(f.Rpc("fight","stage",1,"cardIds",team));Success(f.Rpc("fight","stage",1,"cardIds",team));
        int gems=f.Economy.Snapshot().Gems;Error("FightDailyLimit",f.Rpc("fight","stage",1,"cardIds",team));Check(f.Economy.Snapshot().Gems==gems,"Daily fight cap prevents further currency farming");
        // Prepare a real native upgrade only as fixture setup, then exercise RPC locks.
        var upgrade=f.Economy.PrepareUpgrade(f.Cards[3].CardId,new List<string>{f.Cards[4].CardId},false);
        Check(((HyTcgGameplayProfile)Success(f.Rpc("profile","cardId",f.Cards[3].CardId))["profile"]).Busy,"Upgrade busy profile remains readable through RPC");
        Success(f.Image("cardId",f.Cards[3].CardId,"original",true));
        Error("CardLocked",f.Rpc("date","cardId",f.Cards[3].CardId,"activity","chat"));
        Error("CardLocked",f.Rpc("cosmeticReset","cardId",f.Cards[3].CardId));f.Economy.CancelUpgrade(upgrade.Token);
        string resetDay=f.Now.Date.AddDays(1).ToString("yyyy-MM-dd");f.Now=f.Now.Date.AddHours(17);var reset=Success(f.Rpc("snapshot"));
        Check(State(reset).Day==resetDay && State(reset).Fight.DailyRemaining==3 && State(reset).DateDailyRemaining==6 && State(reset).Profiles.Single(p=>p.CardId==id).Bond>0,"UTC+7 snapshot renews quotas and preserves progression");
    }
    private static void DefaultImagesAndSignedLayouts(Fixture f)
    {
        string id=f.Cards[3].CardId,material=f.Cards[4].CardId,container=HashFile(f.Files[id]);
        var issued=f.Economy.ReadOwnedCardMetadata(id);
        Check(issued.Metadata["hytcg_card_layout"]=="window-v4" && f.Cards[3].Metadata["hytcg_card_layout"]=="window-v4","New lower-rarity issuance durably binds native headerless window-v4 and returns the exact marker to renderer");
        Check(f.Economy.ReadOwnedCardMetadata(f.Cards[0].CardId).Metadata==null || f.Economy.ReadOwnedCardMetadata(f.Cards[0].CardId).Metadata["hytcg_card_layout"]=="full-v4","Native UR issuance does not carry a stale window marker");
        var forged=json.Deserialize<HyTcgCard>(json.Serialize(issued));forged.Metadata.Remove("hytcg_card_layout");
        try {using(f.Economy.BeginCardExport("HC-"+Guid.NewGuid().ToString("N"),forged)){}throw new Exception("Expected issued marker signature rejection");}
        catch(HyTcgException error){Check(error.Code=="CardInvalid","Renderer cannot remove the native signed layout marker from an issued card");}
        SignedFixtureCard(f,id,delegate(HyTcgCard card) {card.Metadata.Remove("hytcg_card_layout");card.Metadata["desc"]="An existing signed companion from the previous full-bleed layout.";});
        string original=(string)Success(f.Image("cardId",id,"original",true))["imageData"],before=HashFile(Path.Combine(f.Root,"hytcg-vault.dat"));
        string rendered=(string)Success(f.Image("cardId",id))["imageData"];
        using(var bitmap=BitmapOf(rendered))Check(bitmap.GetPixel(350,940).ToArgb()==Color.FromArgb(238,234,222).ToArgb() && rendered!=original,"Existing unstyled lower-rarity detail is recomposed with cream footer/description");
        var nativeDetail=Success(f.Native("hytcgReadCard","hytcgReadCard","cardId",id));
        Check((string)nativeDetail["imageData"]==rendered && nativeDetail["cosmetic"]==null,"Actual hytcgReadCard detail dispatcher uses default lower-rarity composition");
        var collection=(HyTcgCollectionPage)Success(f.Native("hytcgCollection","hytcgCollection","offset",0,"rarity","","query",issued.Name))["page"];
        Check(collection.Items.Count==1 && collection.Items[0].Card.CardId==id,"Actual collection RPC returns the owned lower-rarity card");
        using(var bitmap=BitmapOf(collection.Items[0].ImagePath))Check(bitmap.Width==280 && bitmap.Height==392 && bitmap.GetPixel(135,350).R>200 && bitmap.GetPixel(135,350).G>200,"Actual collection RPC supplies a bounded recomposed lower-rarity thumbnail");
        Check((string)Success(f.Image("cardId",id,"original",true))["imageData"]==original && HashFile(f.Files[id])==container && HashFile(Path.Combine(f.Root,"hytcg-vault.dat"))==before,"Default display migration never rewrites protected card, canonical original branch or vault");
        SignedFixtureCard(f,id,delegate(HyTcgCard card){card.Rarity="Rare";card.RarityColor=HyTcgRules.RarityColor("Rare");});
        SignedFixtureCard(f,material,delegate(HyTcgCard card){card.Rarity="SR";card.RarityColor=HyTcgRules.RarityColor("SR");});
        f.Service.SaveConfig(new HyTcgConfig {CharacterIdMin=55,CharacterIdMax=55,MaxCharacterAttempts=2,Concurrency=2,RequestTimeoutSeconds=3,RetryDelayMs=100,ZerochanUsername=""});
        f.Transport.FixtureImage=Png(750,1050,Color.MidnightBlue);
        var prepared=Success(f.Native("hytcgUpgradePrepare","hytcgUpgradeReady","cardId",id,"materialIds",new[]{material},"changeArtwork",false));
        var upgraded=(HyTcgCard)prepared["card"];string token=(string)prepared["upgradeToken"];
        Check(upgraded.Rarity=="SR" && upgraded.Revision==1 && upgraded.Metadata["hytcg_card_layout"]=="window-v4" && f.Economy.GetUpgrade(token).Card.Metadata["hytcg_card_layout"]=="window-v4","Actual upgrade preparation binds headerless SR window layout inside native protected upgrade intent");
        forged=json.Deserialize<HyTcgCard>(json.Serialize(upgraded));forged.Metadata.Remove("hytcg_card_layout");
        try {f.Economy.AuthorizeUpgrade(token,forged);throw new Exception("Expected forged upgrade marker rejection");}
        catch(HyTcgException error){Check(error.Code=="CardInvalid","Renderer cannot remove the signed layout marker from a prepared upgrade");}
        var exportCard=json.Deserialize<HyTcgCard>(json.Serialize(upgraded));exportCard.Metadata.Remove("hytcg_card_layout");
        string png=HyTcgCosmeticRenderer.Render(exportCard,(string)prepared["imageData"],null,null,false);
        Success(f.Native("hytcgUpgradeSave","hytcgUpgradeSaved","upgradeToken",token,"png",png));
        Check(f.Economy.ReadOwnedCardMetadata(id).Metadata["hytcg_card_layout"]=="window-v4","Committed SR upgrade retains its native signed headerless window marker");
        Check((string)Success(f.Image("cardId",id))["imageData"]==(string)Success(f.Image("cardId",id,"original",true))["imageData"],"Native-marked SR upgrade export is displayed once without a second recomposition");
        string ssrMaterial=f.Cards[1].CardId;
        SignedFixtureCard(f,ssrMaterial,delegate(HyTcgCard card){card.Rarity="SSR";card.RarityColor=HyTcgRules.RarityColor("SSR");card.CharacterId=issued.CharacterId;});
        prepared=Success(f.Native("hytcgUpgradePrepare","hytcgUpgradeReady","cardId",id,"materialIds",new[]{ssrMaterial},"changeArtwork",false));upgraded=(HyTcgCard)prepared["card"];token=(string)prepared["upgradeToken"];
        Check(upgraded.Rarity=="SSR" && upgraded.Revision==2 && upgraded.Metadata["hytcg_card_layout"]=="full-v4" && f.Economy.GetUpgrade(token).Card.Metadata["hytcg_card_layout"]=="full-v4","Upgrade crossing SR toSSR clears the signed window marker before renderer produces full bleed");
        Success(f.Native("hytcgUpgradeSave","hytcgUpgradeSaved","upgradeToken",token,"png",Png(750,1050,Color.Teal)));
        Check(f.Economy.ReadOwnedCardMetadata(id).Rarity=="SSR" && f.Economy.ReadOwnedCardMetadata(id).Metadata["hytcg_card_layout"]=="full-v4" && (string)Success(f.Image("cardId",id))["imageData"]==(string)Success(f.Image("cardId",id,"original",true))["imageData"],"Committed SSR upgrade keeps full-bleed canonical image and no low-rarity marker");
        Check(f.Transport.Calls==2,"Upgrade tests use only two fixture source-image calls and no real external API");
    }
    private static void Boundaries(Fixture f,Fixture other)
    {
        string absent=other.Cards[0].CardId,id=f.Cards[0].CardId;int gems=f.Economy.Snapshot().Gems,otherGems=other.Economy.Snapshot().Gems;
        Error("CardAlreadyDissolved",f.Image("cardId",absent));Error("CardAlreadyDissolved",f.Rpc("profile","cardId",absent));
        Error("CardAlreadyDissolved",f.Rpc("date","cardId",absent,"activity","chat"));
        Error("CardAlreadyDissolved",f.Rpc("albumSave","name","Foreign","cardIds",new[]{absent}));
        Error("CardAlreadyDissolved",f.Image("cardId",absent,"artworkId",new string('0',64)));
        Error("CardInvalid",f.Image("cardId","../../hytcg-vault.dat"));
        Error("ArtworkUnavailable",f.Image("cardId",id,"artworkId",new string('0',64)));
        Error("GameplayInvalid",f.Rpc("unknownAction","gems",999999));
        Error("SearchInvalid",f.Rpc("roster","query",new string('x',181)));
        Check(f.Economy.Snapshot().Gems==gems && other.Economy.Snapshot().Gems==otherGems && other.Economy.CollectionCards().Count==1,"Invalid/foreign actions preserve both isolated accounts");
        var stale=Envelope("hytcgLifestyle","inventoryUse","stale-session");stale["session"]="other-valid-session";stale["itemId"]="training";stale["cardId"]=id;Ignored(f,stale,f.Window,"Stale session cannot dispatch mutations or replies");
        var invalid=Envelope("hytcgLifestyle","snapshot","bad-request!");Ignored(f,invalid,f.Window,"Malformed request ID is ignored safely");
        invalid=Envelope("hytcgLifestyle","snapshot","bad-session");invalid["session"]="x";Ignored(f,invalid,f.Window,"Malformed session is ignored safely");
        invalid=Envelope("hytcgLifestyle","snapshot","foreign-document");Ignored(f,invalid,new PluginBrowserWindow(),"Unregistered browser document cannot dispatch or receive replies");
    }
    private static void BusyAndCancellation(Fixture f)
    {
        var gate=(SemaphoreSlim)Field(f.Host,"hyTcgLifestyleGate");gate.Wait();gate.Wait();
        try
        {
            Send(f.Host,f.Window,Envelope("hytcgLifestyle","snapshot","held-duplicate"));
            Send(f.Host,f.Window,Envelope("hytcgLifestyle","snapshot","held-duplicate"));
            Check(Convert.ToString(f.Window.WaitFor("held-duplicate","hytcgLifestyle")["error"])=="Busy","Duplicate in-flight RPC gets Busy");
            Check(((HashSet<string>)Field(f.Host,"hyTcgLifestyleRequests")).Count==1,"Duplicate rejection retains original tracked request");
            for(int i=0;i<7;i++)Send(f.Host,f.Window,Envelope("hytcgLifestyle","snapshot","held-"+i));
            Send(f.Host,f.Window,Envelope("hytcgLifestyle","snapshot","held-overflow"));
            Check(Convert.ToString(f.Window.WaitFor("held-overflow","hytcgLifestyle")["error"])=="Busy" && ((HashSet<string>)Field(f.Host,"hyTcgLifestyleRequests")).Count==8,"RPC queue cap rejects ninth outstanding request");
        }
        finally {gate.Release(2);}
        for(int i=0;i<7;i++)Success(f.Window.WaitFor("held-"+i,"hytcgLifestyle"));
        var replies=Replies(f.Window);bool completed=false;
        for(int i=0;i<1000 && !completed;i++)
        {lock(replies)completed=replies.Any(r=>Convert.ToString(r["requestId"])=="held-duplicate" && Convert.ToString(r["error"])=="");if(!completed)Thread.Sleep(10);}
        Check(completed && gate.CurrentCount==2 && ((HashSet<string>)Field(f.Host,"hyTcgLifestyleRequests")).Count==0,"Bounded queued RPCs finish and restore native resources");
        int gems=f.Economy.Snapshot().Gems;var inventory=((HyTcgGameplayPage)f.Economy.GameplaySnapshot()).Inventory.Single(i=>i.Id=="training").Count;
        f.Host.Cancel();Error("TimedOut",f.Rpc("inventoryUse","itemId","training","cardId",f.Cards[0].CardId));
        Check(f.Economy.Snapshot().Gems==gems && ((HyTcgGameplayPage)f.Economy.GameplaySnapshot()).Inventory.Single(i=>i.Id=="training").Count==inventory,"Canceled session cannot consume items or award currency");
    }
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);
            if(!String.Equals(root.TrimEnd(Path.DirectorySeparatorChar),AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)
                || root.IndexOf("hytcg-lifestyle-bridge-",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Verifier requires its own generated output fixture directory");
            var fixture=new Fixture(Path.Combine(root,"account-a"),5);var other=new Fixture(Path.Combine(root,"account-b"),1);
            var hashes=fixture.Files.ToDictionary(p=>p.Key,p=>HashFile(p.Value));string otherHash=HashFile(other.Files.Values.Single());
            BasicAndCreative(fixture);
            StudioCreationAndPreview(new Fixture(Path.Combine(root,"account-studio"),3),other);
            LegacyStudio(new Fixture(Path.Combine(root,"account-legacy"),1));
            DefaultImagesAndSignedLayouts(new Fixture(Path.Combine(root,"layout"),5));
            Gameplay(fixture);Boundaries(fixture,other);BusyAndCancellation(fixture);
            Check(fixture.Files.All(p=>HashFile(p.Value)==hashes[p.Key]) && HashFile(other.Files.Values.Single())==otherHash,"All bridge actions preserve original protected card containers");
            Check(fixture.Economy.CollectionCards().Count==5 && fixture.Transport.Calls==0 && other.Transport.Calls==0,"Fixture remains owned and all bridge verification stays offline");
            Console.WriteLine("HYTCG actual lifestyle/image RPC bridge PASS · "+checks+" checks · fixture-only/offline");return 0;
        }
        catch(Exception error) {Console.Error.WriteLine(error);return 1;}
    }
}

