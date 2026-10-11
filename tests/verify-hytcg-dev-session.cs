using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

public static class HyTcgDevSessionCheck
{
    private static int checks;
    private static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private static object Call(object target,string method,params object[] args)
    {
        var match=target.GetType().GetMethods(Flags).Where(m=>m.Name==method && m.GetParameters().Length==args.Length).First(m=>m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(x=>x));
        try{return match.Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException;}
    }
    private static object Value(object target,string name)
    {var p=target.GetType().GetProperty(name,Flags);return p==null?target.GetType().GetField(name,Flags).GetValue(target):p.GetValue(target,null);}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    private static void Fails(string code,Action action){try{action();throw new Exception("Expected "+code);}catch(Exception e){if(e.GetType().Name!="HyTcgException" || (string)e.GetType().GetField("Code",Flags).GetValue(e)!=code)throw;checks++;}}
    private static string Digest(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
    private static Dictionary<string,string> Hashes(string root){return Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(f=>f.Substring(root.Length),Digest);}
    public static object Run(string candidate,string testRoot)
    {
        checks=0;var assembly=Assembly.LoadFrom(candidate);var serviceType=assembly.GetType("HYROLauncherWeb.HyTcgService");var sessionType=assembly.GetType("HYROLauncherWeb.HyTcgDevSession");
        // The economy fixture includes a real signed card and protected custom
        // drawing, then validates resource actions against an isolated clone.
        var economyCheck=HyTcgDevEconomyCheck.Run(candidate,Path.Combine(testRoot,"economy"));
        string realRoot=Path.Combine(testRoot,"economy","real-fixture"),configRoot=Path.Combine(testRoot,"config"),sessions=Path.Combine(testRoot,"sessions");
        Directory.CreateDirectory(Path.Combine(configRoot,"assets","apikey"));
        File.WriteAllText(Path.Combine(configRoot,"assets","apikey","hytcgconfig.json"),"{\"CharacterIdMin\":1,\"CharacterIdMax\":999999,\"MaxCharacterAttempts\":20,\"Concurrency\":2,\"RequestTimeoutSeconds\":10,\"RetryDelayMs\":100}");
        File.WriteAllText(Path.Combine(configRoot,"assets","apikey","zerochanuser.txt"),"HYTCG-Test");
        var real=Activator.CreateInstance(serviceType,Flags,null,new object[]{realRoot},CultureInfo.InvariantCulture);var realEconomy=Value(real,"Economy");
        var realHashes=Hashes(realRoot);var configHashes=Hashes(configRoot);
        Directory.CreateDirectory(sessions);string sibling=Path.Combine(sessions,"preserve-me.txt");File.WriteAllText(sibling,"separate session parent data");
        TimeSpan elapsed=TimeSpan.Zero;var clock=new Func<TimeSpan>(delegate{return elapsed;});
        var session=Activator.CreateInstance(sessionType,Flags,null,new object[]{real,configRoot,sessions,clock},CultureInfo.InvariantCulture);
        string devRoot=(string)Value(session,"DirectoryPath");var dev=Value(session,"Service");var devEconomy=Value(dev,"Economy");
        Check((int)Value(session,"RemainingSeconds")==600,"Session did not begin at 600 seconds");
        Check(!(bool)Value(session,"Expired"),"Session began expired");
        Check(Directory.Exists(devRoot) && Path.GetDirectoryName(devRoot)==sessions,"Sandbox path escaped configured parent");
        Check((string)Value(dev,"CardsDirectory")==Path.Combine(devRoot,"cards"),"Dev cards remain in real directory");
        Check(Object.ReferenceEquals(Value(session,"RealService"),real),"Source service not preserved");
        var config=Call(dev,"LoadConfig");Check((int)Value(config,"CharacterIdMax")==999999,"Dev read wrong config root");
        Check((string)Value(config,"ZerochanUsername")=="HYTCG-Test","Dev ignored read-only Zerochan username");
        Fails("DevReadOnlyConfig",delegate{Call(dev,"SaveConfig",config);});
        Check(configHashes.OrderBy(p=>p.Key).SequenceEqual(Hashes(configRoot).OrderBy(p=>p.Key)),"Dev modified source configuration");
        string abandoned=Path.Combine(sessions,Guid.NewGuid().ToString("N")),pidReuse=Path.Combine(sessions,Guid.NewGuid().ToString("N"));
        string recent=Path.Combine(sessions,Guid.NewGuid().ToString("N")),old=Path.Combine(sessions,Guid.NewGuid().ToString("N")),unknown=Path.Combine(sessions,Guid.NewGuid().ToString("N"));
        foreach(string directory in new[]{abandoned,pidReuse,recent,old,unknown})Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(abandoned,".dev-owner"),"2147483647\n0");
        using(var process=Process.GetCurrentProcess())File.WriteAllText(Path.Combine(pidReuse,".dev-owner"),process.Id+"\n"+(process.StartTime.ToUniversalTime().Ticks-1));
        File.WriteAllText(Path.Combine(unknown,".dev-owner"),"invalid ownership record");Directory.SetCreationTimeUtc(old,DateTime.UtcNow.AddDays(-2));
        Call(session,"CleanupStale",sessions);
        Check(Directory.Exists(devRoot),"Startup cleanup deleted an active process's dev session");
        Check(!Directory.Exists(abandoned),"Startup retained a dead owner's resources");
        Check(!Directory.Exists(pidReuse),"PID reuse incorrectly protected an abandoned session");
        Check(Directory.Exists(recent),"Startup raced a fresh session before ownership marker registration");
        Check(!Directory.Exists(old),"Startup retained an old session interrupted before ownership registration");
        Check(Directory.Exists(unknown) && File.Exists(sibling),"Startup cleanup removed unrecognized neighboring data");
        elapsed=TimeSpan.FromSeconds(599.999);Check(!(bool)Value(session,"Expired"),"Session expires early");
        Check((int)Value(session,"RemainingSeconds")==1,"Countdown did not round up");Call(devEconomy,"Snapshot");
        elapsed=TimeSpan.FromSeconds(600);Check((bool)Value(session,"Expired"),"Exact ten-minute deadline accepted work");Check((int)Value(session,"RemainingSeconds")==0,"Expired countdown nonzero");
        Fails("DevExpired",delegate{Call(devEconomy,"BuyCredit");});
        Check(realHashes.OrderBy(p=>p.Key).SequenceEqual(Hashes(realRoot).OrderBy(p=>p.Key)),"Lifetime test changed real progress files");
        elapsed=TimeSpan.Zero;Call(session,"Close");Check((bool)Value(session,"Expired"),"Explicit close failed");Fails("DevExpired",delegate{Call(devEconomy,"Snapshot");});
        ((IDisposable)session).Dispose();Check(!Directory.Exists(devRoot),"Dispose left temporary resources");
        Check(File.Exists(sibling) && File.ReadAllText(sibling)=="separate session parent data","Cleanup removed neighboring files");
        Check(realHashes.OrderBy(p=>p.Key).SequenceEqual(Hashes(realRoot).OrderBy(p=>p.Key)),"Dispose changed real account data");
        Check(configHashes.OrderBy(p=>p.Key).SequenceEqual(Hashes(configRoot).OrderBy(p=>p.Key)),"Dispose changed configuration");
        Check(!(bool)Value(realEconomy,"IsDevMode"),"Source service remains in dev mode");
        return new {Status="PASS",Checks=checks,Economy=economyCheck,TestRoot=testRoot};
    }
}
