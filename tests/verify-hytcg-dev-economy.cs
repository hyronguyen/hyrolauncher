using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

public static class HyTcgDevEconomyCheck
{
    private static int checks;
    private static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private static object Call(object target,string method,params object[] args)
    {
        var match=target.GetType().GetMethods(Flags).Where(m=>m.Name==method && m.GetParameters().Length==args.Length).FirstOrDefault(m=>m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(x=>x));
        if(match==null)throw new Exception("Missing method "+method);
        try{return match.Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException;}
    }
    private static object Value(object target,string property) {return target.GetType().GetProperty(property,Flags).GetValue(target,null);}
    private static void Set(object target,string property,object value) {target.GetType().GetProperty(property,Flags).SetValue(target,value,null);}
    private static void Check(bool result,string message){if(!result)throw new Exception(message);checks++;}
    private static void Fails(string code,Action action){try{action();throw new Exception("Expected "+code);}catch(Exception e){if(e.GetType().Name!="HyTcgException" || (string)e.GetType().GetField("Code",Flags).GetValue(e)!=code)throw;checks++;}}
    private static string Digest(string file){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(file)));}
    private static Dictionary<string,string> StateHashes(string root)
    {return Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(f=>f.Substring(root.Length),Digest,StringComparer.Ordinal);}
    private static string Operation(object creative)
    {return "SC-"+Value(creative,"NextStudioSequence")+"-"+Guid.NewGuid().ToString("N");}
    private static object Input(params object[] pairs)
    {var input=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)input.Add((string)pairs[i],pairs[i+1]);return input;}
    private static object Card(Assembly assembly)
    {
        var json="{\"CharacterId\":99991,\"Name\":\"Sandbox test\",\"Series\":\"HYTCG verification\",\"Gender\":\"Female\",\"Rarity\":\"Common\",\"RarityColor\":\"#94A3B8\",\"ImageSource\":\"acdb\",\"SourceImage\":\"https://www.animecharactersdatabase.com/test.png\",\"ProfileImage\":\"https://www.animecharactersdatabase.com/test.png\",\"CardId\":\"HT-20261011-123456ABCDEF\",\"CreatedAt\":\"2026-10-11T12:00:00Z\",\"Orientation\":\"portrait\"}";
        return new JavaScriptSerializer().Deserialize(json,assembly.GetType("HYROLauncherWeb.HyTcgCard"));
    }
    public static object Run(string candidate,string testRoot)
    {
        checks=0;var assembly=Assembly.LoadFrom(candidate);var economyType=assembly.GetType("HYROLauncherWeb.HyTcgEconomy");
        var sourceRoot=Path.Combine(testRoot,"real-fixture");Directory.CreateDirectory(sourceRoot);
        var source=Activator.CreateInstance(economyType,Flags,null,new object[]{sourceRoot},CultureInfo.InvariantCulture);
        Check(!(bool)Value(source,"IsDevMode"),"Fresh real economy entered dev");
        Check((int)Value(Call(source,"Snapshot"),"Gems")==1800,"Normal starting wallet changed");
        var reservation=Call(source,"Reserve",1,"gems");var token=((List<string>)Value(reservation,"Tokens"))[0];Call(source,"Begin",token);
        var card=Card(assembly);Call(source,"AuthorizeCard",token,card);
        var storage=assembly.GetType("HYROLauncherWeb.HyTcgStorage");var pngType=assembly.GetType("HYROLauncherWeb.HyTcgService+Png");
        byte[] png;using(var bitmap=new Bitmap(750,1050))using(var graphics=Graphics.FromImage(bitmap))using(var bytes=new MemoryStream())
        {graphics.Clear(Color.CornflowerBlue);bitmap.Save(bytes,ImageFormat.Png);png=(byte[])pngType.GetMethod("WithMetadata",Flags).Invoke(null,new object[]{bytes.ToArray(),new JavaScriptSerializer().Serialize(card)});}
        var container=(byte[])storage.GetMethod("Protect",Flags).Invoke(null,new object[]{png});Directory.CreateDirectory(Path.Combine(sourceRoot,"cards"));File.WriteAllBytes(Path.Combine(sourceRoot,"cards","test.hycard"),container);
        Call(source,"Complete",token,card,"test.hycard",container);
        string artwork=(string)Call(source,"StoreCustomArtwork",png);
        var initialCreative=Call(source,"CreativeSnapshot");
        Call(source,"CreativeAction","cosmeticSave",Input("operationId",Operation(initialCreative),"cardId",Value(card,"CardId"),"artworkId",artwork));
        int realGems=(int)Value(Call(source,"Snapshot"),"Gems");
        var normalProfile=Call(source,"GameplayProfileSnapshot",Value(card,"CardId"));Check((int)Value(normalProfile,"Level")==1,"Real card level changed");
        var normalCreative=Call(source,"CreativeSnapshot");Check(((System.Collections.ICollection)Value(normalCreative,"Dyes")).Count==0,"Real dyes changed");
        Fails("GemsInsufficient",delegate {Call(source,"CreativeAction","albumCreate",Input("operationId",Operation(normalCreative),"name","No free real album"));});
        Fails("DevUnavailable",delegate {Call(source,"EnsureDevUpgradeMaterial",Value(card,"CardId"));});
        var hashes=StateHashes(sourceRoot);var sandbox=Path.Combine(testRoot,"dev-session");
        var dev=Call(source,"CreateDevSandbox",sandbox);
        Check((bool)Value(dev,"IsDevMode"),"Sandbox was not dev");
        Check((string)Call(dev,"ReadCustomArtwork",artwork)==(string)Call(source,"ReadCustomArtwork",artwork),"Custom protected artwork missing or changed in clone");
        bool alive=true;Set(dev,"DevSessionAlive",new Func<bool>(delegate {return alive;}));
        var wallet=Call(dev,"Snapshot");Check((int)Value(wallet,"Gems")>=100000000,"Gems not replenished");Check((int)Value(wallet,"PaidCredits")>=100000,"Credits not replenished");Check((int)Value(wallet,"LimitedCredits")>=100000,"Limited credits not replenished");
        var profile=Call(dev,"GameplayProfileSnapshot",Value(card,"CardId"));Check((int)Value(profile,"Level")==30,"Skills remain level locked");
        var creative=Call(dev,"CreativeSnapshot");Check(((System.Collections.ICollection)Value(creative,"Dyes")).Count==128,"All dyes including mystic unavailable");
        Check(((System.Collections.ICollection)Value(creative,"FrameItems")).Count==0 && (string)Value(creative,"FrameMode")=="rarity","Dev must also use rarity frames without retired frame items");
        var devReservation=Call(dev,"Reserve",10,"gems");var devTokens=(List<string>)Value(devReservation,"Tokens");Check(devTokens.Count==10,"Dev x10 rejected");
        foreach(string devToken in devTokens)Call(dev,"Refund",devToken);
        for(int i=0;i<10;i++)
        {
            var explore=Call(dev,"GameplayAction","expeditionStart",Input("routeId","ruins","cardIds",new string[]{(string)Value(card,"CardId")}));
            string expedition=(string)Value(Value(explore,"Result"),"ExpeditionId");Call(dev,"GameplayAction","expeditionClaim",Input("expeditionId",expedition));
        }
        Check((int)Value(Call(dev,"GameplaySnapshot"),"ExpeditionStartsRemaining")==6,"Dev expedition quota exhausted");
        for(int i=0;i<12;i++)Call(dev,"GameplayAction","date",Input("cardId",Value(card,"CardId"),"activity","gift"));
        Check((int)Value(Call(dev,"GameplaySnapshot"),"DateDailyRemaining")==6,"Dev gift quota exhausted");
        for(int i=0;i<5;i++)
        {
            var battle=Call(dev,"GameplayAction","fight",Input("stage",20,"cardIds",new string[]{(string)Value(card,"CardId")}));
            var battleState=Value(Value(battle,"State"),"Battle");Call(dev,"GameplayAction","battleForfeit",Input("battleId",Value(battleState,"Id")));
        }
        Check((int)Value(Value(Call(dev,"GameplaySnapshot"),"Fight"),"DailyRemaining")==3,"Dev arena quota exhausted");
        creative=Call(dev,"CreativeSnapshot");Call(dev,"CreativeAction","albumCreate",Input("operationId",Operation(creative),"name","Temporary album"));
        Check(((System.Collections.ICollection)Value(Call(dev,"CreativeSnapshot"),"Albums")).Count==1,"Dev album did not save to sandbox");
        string material=(string)Call(dev,"EnsureDevUpgradeMaterial",Value(card,"CardId"));Check(!String.IsNullOrEmpty(material),"Upgrade material absent");
        Call(dev,"PrepareUpgrade",Value(card,"CardId"),new List<string>{material},false);Check(((System.Collections.ICollection)Call(dev,"PendingUpgrades")).Count==1,"Dev upgrade unavailable");
        Check(hashes.OrderBy(p=>p.Key).SequenceEqual(StateHashes(sourceRoot).OrderBy(p=>p.Key)),"Dev mutated production fixture files");
        string copiedDev=Path.Combine(testRoot,"dev-file-replay");Directory.CreateDirectory(copiedDev);
        File.Copy(Path.Combine(sandbox,"hytcg-vault.dat"),Path.Combine(copiedDev,"hytcg-vault.dat"));
        File.Copy(Path.Combine(sandbox,"hytcg-vault.stamp"),Path.Combine(copiedDev,"hytcg-vault.stamp"));
        Fails("VaultInvalid",delegate {
            try {Activator.CreateInstance(economyType,Flags,null,new object[]{copiedDev},CultureInfo.InvariantCulture);}
            catch(TargetInvocationException e){throw e.InnerException;}
        });
        var devHashes=StateHashes(sandbox);alive=false;
        Fails("DevExpired",delegate {Call(dev,"Snapshot");});
        Fails("DevExpired",delegate {Call(dev,"CreativeAction","dyeBoosterBuy",Input("operationId",Operation(Call(source,"CreativeSnapshot"))));});
        Check(devHashes.OrderBy(p=>p.Key).SequenceEqual(StateHashes(sandbox).OrderBy(p=>p.Key)),"Expired dev wrote files");
        alive=true;int lifetimeChecks=0;Set(dev,"DevSessionAlive",new Func<bool>(delegate {return ++lifetimeChecks<3;}));
        Fails("DevExpired",delegate {Call(dev,"SetFavorite",Value(card,"CardId"),true);});
        Check(devHashes.OrderBy(p=>p.Key).SequenceEqual(StateHashes(sandbox).OrderBy(p=>p.Key)),"Deadline crossed inside a transaction still committed");
        Set(dev,"DevSessionAlive",new Func<bool>(delegate {return alive;}));Call(dev,"CloseDevMode");Fails("DevExpired",delegate {Call(dev,"Snapshot");});
        var restored=Activator.CreateInstance(economyType,Flags,null,new object[]{sourceRoot},CultureInfo.InvariantCulture);
        Check(!(bool)Value(restored,"IsDevMode"),"Real economy restored in dev");Check((int)Value(Call(restored,"Snapshot"),"Gems")==realGems,"Real gems changed");
        Check((int)Value(Call(restored,"GameplayProfileSnapshot",Value(card,"CardId")),"Level")==1,"Temporary levels leaked");
        Fails("DevSandboxInvalid",delegate {Call(source,"CreateDevSandbox",sourceRoot);});Fails("DevSandboxInvalid",delegate {Call(source,"CreateDevSandbox",Path.Combine(sourceRoot,"nested"));});
        return new {Status="PASS",Checks=checks,TestRoot=testRoot};
    }
}
