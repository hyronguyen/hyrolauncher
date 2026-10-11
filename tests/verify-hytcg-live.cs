using System;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using HYROLauncherWeb;
internal static class HyTcgLiveProbe
{
    public static int Main(string[] args)
    {
        ApiActivityLog.Start(args[0]);try{return Run(args);}finally{ApiActivityLog.Stop();}
    }
    private static int Run(string[] args)
    {
        var transport=new HyTcgTransport();
        var service=new HyTcgService(args[0]);
        string configRoot=args.Length>1?args[1]:Environment.GetEnvironmentVariable("HYRO_TEST_CONFIG_ROOT")??Directory.GetCurrentDirectory();
        service.UseReadOnlyConfig(configRoot);
        HyTcgConfig config;
        try
        {
            config=service.LoadConfig();
            if(String.IsNullOrWhiteSpace(config.ZerochanUsername))throw new HyTcgException("ZerochanUsernameMissing");
        }
        catch(Exception ex){Console.Error.WriteLine("Live test configuration failed: "+ex.Message);return 1;}
        string agent="HYTCG - "+config.ZerochanUsername;
        try
        {
            HyTcgService.WaitForZerochan(CancellationToken.None);
            var entries=HyTcgService.ParseArtworkXml(transport.Json("https://www.zerochan.net/Hinata+Hyuuga?xml&strict&l=50",agent,12,CancellationToken.None));
            if(entries.Count==0) throw new Exception("Empty Zerochan response");
            Console.WriteLine("PASS: native Zerochan XML API, username User-Agent, alias redirect preserves XML/strict ("+entries.Count+" entries).");
            HyTcgService.WaitForZerochan(CancellationToken.None);
            var detail=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(transport.Json("https://www.zerochan.net/"+Convert.ToString(entries[0]["id"])+"?json",agent,12,CancellationToken.None));
            string image=transport.Image((string)detail["full"],agent,12,CancellationToken.None);
            Directory.CreateDirectory(args[0]);File.WriteAllText(Path.Combine(args[0],"artwork-data.txt"),image);
            Console.WriteLine("PASS: native artwork download / validated image bytes.");
        }
        catch(Exception ex) {Console.Error.WriteLine("Zerochan live failure: "+ex.Message);return 1;}
        try
        {
            string json=transport.Json(HyTcgService.AcdbEndpoint+"?character_id=99991","HYTCG/1.0 (HYRO Launcher)",12,CancellationToken.None);
            var card=HyTcgService.ParseCharacter(json,99991);
            if(card==null)throw new Exception("ACDB did not return a valid character");
            Console.WriteLine("PASS: documented ACDB curl API access, character #"+card.CharacterId+": "+card.Name+" / "+card.Series);
            string profile=transport.Image(card.ProfileImage,"HYTCG/1.0 (HYRO Launcher)",12,CancellationToken.None);
            File.WriteAllText(Path.Combine(args[0],"acdb-profile-data.txt"),profile);
            Console.WriteLine("PASS: ACDB profile image through the same rate-limited client.");
            config.CharacterIdMin=99991;config.CharacterIdMax=99991;
            using(var cancel=new CancellationTokenSource(60000))
            {
                var opened=service.Open(config,(phase,attempt)=>{},cancel.Token);
                if(opened.Card.ImageSource!="zerochan")throw new Exception("Known character did not produce ALT: "+opened.Card.FallbackReason);
                File.WriteAllText(Path.Combine(args[0],"live-card.json"),new JavaScriptSerializer().Serialize(opened.Card));
                File.WriteAllText(Path.Combine(args[0],"live-card-image.txt"),opened.ImageData);
                Console.WriteLine("PASS: complete live Open flow, local rarity, ACDB character and "+opened.Card.ImageSource+" artwork: "+opened.Card.Name+" ("+opened.Card.Rarity+").");
            }
        }
        catch(Exception ex) {Console.Error.WriteLine("ACDB live failure: "+ex.Message);return 1;}
        return 0;
    }
}
