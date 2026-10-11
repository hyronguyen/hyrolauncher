using System.Collections.Generic;

namespace HYROLauncherWeb
{
    internal static class HyTcgRules
    {
        internal const int FreeIntervalSeconds=20*60;
        internal const int MaxFreeCredits=5;
        internal const int PackPrice=160;
        internal const int LimitedPackPrice=180;
        internal const int PitySR=50;
        internal const int PitySSR=100;
        internal const int PityUR=500;
        internal static List<HyTcgRate> DropRates()
        {
            // Fixed base odds; native successful card authorizations apply the hard pity below.
            return new List<HyTcgRate> {
                new HyTcgRate{Name="Common",Weight=65,Color="#b1becd"},
                new HyTcgRate{Name="Uncommon",Weight=22,Color="#78e2af"},
                new HyTcgRate{Name="Rare",Weight=7.3,Color="#6db7ff"},
                new HyTcgRate{Name="SR",Weight=4,Color="#c29bff"},
                new HyTcgRate{Name="SSR",Weight=1.1,Color="#ffc76e"},
                new HyTcgRate{Name="UR",Weight=.6,Color="#ff8dcc"}
            };
        }
        internal static int SalvageValue(string rarity)
        {
            switch(rarity) { case "Common":return 1;case "Uncommon":return 3;case "Rare":return 8;case "SR":return 20;case "SSR":return 60;case "UR":return 160;default:return 0; }
        }
        internal static int RarityIndex(string rarity) { return System.Array.IndexOf(HyTcgService.Rarities,rarity); }
        internal static string RarityColor(string rarity)
        { foreach(var rate in DropRates()) if(rate.Name==rarity) return rate.Color; throw new HyTcgException("CardInvalid"); }
    }
}
