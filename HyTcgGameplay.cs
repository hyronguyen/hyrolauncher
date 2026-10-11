using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace HYROLauncherWeb
{
    // Only this small progression record is persisted. Stats and rewards are derived by
    // native code from authenticated ownership; renderer-supplied values are never used.
    public sealed class HyTcgCompanionProgress
    {
        public List<string> Skills { get; set; }
        public int Xp { get; set; }
        public int Bond { get; set; }
        public string DateDay { get; set; }
        public int Dates { get; set; }
    }
    public sealed class HyTcgExpeditionRecord
    {
        public string Id { get; set; }
        public string RouteId { get; set; }
        public List<string> CardIds { get; set; }
        public string StartedAt { get; set; }
        public string EndsAt { get; set; }
        public bool Claimed { get; set; }
        public string ClaimedAt { get; set; }
        public int Effort { get; set; }
        public int RewardGems { get; set; }
    }
    public sealed class HyTcgGameplayEvent
    {
        public string Id { get; set; }
        public string At { get; set; }
        public string Kind { get; set; }
        public string Message { get; set; }
    }
    public sealed class HyTcgGameplayState
    {
        public HyTcgArenaBattle Battle { get; set; }
        public int Version { get; set; }
        public string Day { get; set; }
        public int ExpeditionStarts { get; set; }
        public int FightAttempts { get; set; }
        public int DateActions { get; set; }
        public int HighestClearedStage { get; set; }
        public Dictionary<string,int> Inventory { get; set; }
        public Dictionary<string,HyTcgCompanionProgress> Profiles { get; set; }
        public List<HyTcgExpeditionRecord> Expeditions { get; set; }
        public List<HyTcgGameplayEvent> History { get; set; }
        public HyTcgGameplayOutcome LastFight { get; set; }
    }
    public sealed class HyTcgGameplayItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public string Kind { get; set; }
        public string Description { get; set; }
    }
    public sealed class HyTcgGameplayRoute
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int DurationSeconds { get; set; }
        public int MinEffort { get; set; }
        public int RewardGems { get; set; }
        public int RewardXp { get; set; }
        public List<HyTcgGameplayItem> RewardItems { get; set; }
        public string Description { get; set; }
    }
    public sealed class HyTcgGameplayExpedition
    {
        public string Id { get; set; }
        public string RouteId { get; set; }
        public List<string> CardIds { get; set; }
        public string StartedAt { get; set; }
        public string EndsAt { get; set; }
        public bool Claimed { get; set; }
        public bool Ready { get; set; }
        public int Effort { get; set; }
        public int RewardGems { get; set; }
        public int RewardXp { get; set; }
        public List<HyTcgGameplayItem> RewardItems { get; set; }
    }
    public sealed class HyTcgGameplayProfile
    {
        public List<string> EquippedSkills { get; set; }
        public List<HyTcgArenaSkill> Skills { get; set; }
        public string CardId { get; set; }
        public string Name { get; set; }
        public string Rarity { get; set; }
        public int CharacterId { get; set; }
        public int Level { get; set; }
        public int Xp { get; set; }
        public int NextLevelXp { get; set; }
        public int XpToNextLevel { get; set; }
        public int Effort { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Hp { get; set; }
        public int Bond { get; set; }
        public int BondLevel { get; set; }
        public bool Busy { get; set; }
        public int DatesRemaining { get; set; }
    }
    public sealed class HyTcgGameplayFight
    {
        public int Stage { get; set; }
        public int MaxStage { get; set; }
        public int HighestClearedStage { get; set; }
        public int DailyRemaining { get; set; }
        public int DailyLimit { get; set; }
        public string EnemyName { get; set; }
        public int EnemyHp { get; set; }
        public int EnemyAttack { get; set; }
        public int EnemyDefense { get; set; }
        public HyTcgGameplayOutcome LastResult { get; set; }
    }
    public sealed class HyTcgGameplayOutcome
    {
        public string Kind { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool Win { get; set; }
        public int Stage { get; set; }
        public int Rounds { get; set; }
        public string EnemyName { get; set; }
        public int EnemyHp { get; set; }
        public int GemsAwarded { get; set; }
        public int XpAwarded { get; set; }
        public int BondAwarded { get; set; }
        public string ExpeditionId { get; set; }
        public List<string> CardIds { get; set; }
        public List<HyTcgGameplayItem> Items { get; set; }
        public List<string> CombatLog { get; set; }
    }
    public sealed class HyTcgGameplayPage
    {
        public HyTcgArenaBattle Battle { get; set; }
        public string Day { get; set; }
        public string ServerNow { get; set; }
        public string NextResetAt { get; set; }
        public int ExpeditionStartsRemaining { get; set; }
        public int ExpeditionDailyLimit { get; set; }
        public int ExpeditionActiveLimit { get; set; }
        public int DateDailyRemaining { get; set; }
        public int DateDailyLimit { get; set; }
        public int DateCardLimit { get; set; }
        public List<HyTcgGameplayRoute> Routes { get; set; }
        public List<HyTcgGameplayExpedition> Expeditions { get; set; }
        public List<HyTcgGameplayItem> Inventory { get; set; }
        public List<HyTcgGameplayProfile> Profiles { get; set; }
        public int ProfilesTotal { get; set; }
        public bool ProfilesHasMore { get; set; }
        public HyTcgGameplayFight Fight { get; set; }
        public List<HyTcgGameplayEvent> History { get; set; }
    }
    public sealed class HyTcgGameplayRosterPage
    {
        public string Query { get; set; }
        public List<HyTcgGameplayProfile> Items { get; set; }
        public int Total { get; set; }
        public int Offset { get; set; }
        public int NextOffset { get; set; }
        public bool HasMore { get; set; }
    }
    public sealed class HyTcgGameplayActionResult
    {
        public HyTcgGameplayPage State { get; set; }
        public HyTcgGameplayOutcome Result { get; set; }
        public HyTcgWallet Wallet { get; set; }
    }
    public sealed class HyTcgArenaSkill
    {
        public string Id { get; set; } public string Name { get; set; } public string Kind { get; set; }
        public int Level { get; set; } public int Energy { get; set; } public int Power { get; set; }
    }
    public sealed class HyTcgArenaUnit
    {
        public string CardId { get; set; } public string Name { get; set; } public int Hp { get; set; } public int MaxHp { get; set; }
        public int Attack { get; set; } public int Defense { get; set; } public int Energy { get; set; }
        public bool Shield { get; set; } public int Focus { get; set; } public List<string> Skills { get; set; }
    }
    public sealed class HyTcgArenaEvent
    {
        public int Index { get; set; } public string ActorId { get; set; } public string TargetId { get; set; }
        public string Kind { get; set; } public string SkillId { get; set; } public int Amount { get; set; } public int HpAfter { get; set; }
    }
    public sealed class HyTcgArenaBattle
    {
        public string Id { get; set; } public int Stage { get; set; } public int Turn { get; set; } public int Actor { get; set; }
        public string StartedAt { get; set; } public bool Complete { get; set; } public bool Win { get; set; }
        public int EnemyHp { get; set; } public int EnemyMaxHp { get; set; } public string EnemyName { get; set; }
        public List<HyTcgArenaUnit> Team { get; set; } public List<HyTcgArenaEvent> Events { get; set; } public List<string> Log { get; set; }
        public string LastInput { get; set; } public int LastTurn { get; set; }
    }

    internal sealed partial class HyTcgEconomy
    {
        private const int GameplayExpeditionDailyLimit=6,GameplayExpeditionActiveLimit=3;
        private const int GameplayFightDailyLimit=3,GameplayDateDailyLimit=6,GameplayDateCardLimit=2;
        private const int GameplayMaxStage=20,GameplayMaxLevel=30,GameplayMaxXp=26100,GameplayMaxBond=1000,GameplayMaxItem=999;
        private static readonly string[] GameplaySupplyIds={"training","tea","gift"};
        private static readonly string[] GameplayItemIds={"training","tea","gift","fiber","petal","crystal"};

        internal object GameplaySnapshot()
        {
            return Write(delegate(Vault v,DateTime now) { EnsureGameplay(v,now);return GameplayPage(v,now); });
        }
        internal object GameplayRoster(string query,int offset,int count)
        {
            if(query==null)query="";query=query.Trim();
            if(query.Length>80 || offset<0 || offset>MaxCards || count<1 || count>500)throw new HyTcgException("GameplayInvalid");
            return Write(delegate(Vault v,DateTime now) {
                EnsureGameplay(v,now);
                var cards=GameplayRosterCards(v,query).ToList();int start=Math.Min(offset,cards.Count);
                var items=cards.Skip(start).Take(count).Select(c=>GameplayProfile(v,c)).ToList();int next=start+items.Count;
                return new HyTcgGameplayRosterPage {Query=query,Items=items,Total=cards.Count,Offset=start,NextOffset=next,HasMore=next<cards.Count};
            });
        }
        internal object GameplayProfileSnapshot(string cardId)
        {
            if(!GameplayCardId(cardId))throw new HyTcgException("CardInvalid");
            return Write(delegate(Vault v,DateTime now) {
                EnsureGameplay(v,now);
                var card=v.Cards.FirstOrDefault(c=>c.CardId==cardId && !c.Dissolved && !c.Legacy);
                if(card==null)throw new HyTcgException("CardUnavailable");
                // This is a metadata preview. Action methods verify PNG/container bytes lazily.
                return GameplayProfile(v,card);
            });
        }
        private static IEnumerable<Owned> GameplayRosterCards(Vault v,string query)
        {
            return v.Cards.AsEnumerable().Reverse().Where(c=>!c.Dissolved && !c.Legacy && (query.Length==0
                || (c.Card.Name??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0
                || (c.Card.Series??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0
                || c.CardId.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0
                || c.Card.Rarity.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0
                || c.Card.CharacterId.ToString(CultureInfo.InvariantCulture)==query));
        }
        internal object GameplayAction(string action,Dictionary<string,object> input)
        {
            if(input==null || input.Count>8)throw new HyTcgException("GameplayInvalid");
            if(action!="expeditionStart" && action!="expeditionClaim" && action!="inventoryUse" && action!="fight" && action!="date" && action!="battleTurn" && action!="battleForfeit" && action!="skillsSave")throw new HyTcgException("GameplayInvalid");
            return Write(delegate(Vault v,DateTime now) {
                EnsureGameplay(v,now);
                HyTcgGameplayOutcome outcome;
                if(action=="expeditionStart")outcome=GameplayStart(v,now,input);
                else if(action=="expeditionClaim")outcome=GameplayClaim(v,now,input);
                else if(action=="inventoryUse")outcome=GameplayUse(v,now,input);
                else if(action=="fight")outcome=GameplayBattle(v,now,input);
                else if(action=="battleTurn")outcome=ArenaTurn(v,now,input);
                else if(action=="battleForfeit")outcome=ArenaForfeit(v,now,input);
                else if(action=="skillsSave")outcome=ArenaSkillsSave(v,input);
                else outcome=GameplayDate(v,now,input);
                if(outcome.Kind!="battleTurn")GameplayHistory(v.Gameplay,now,outcome.Kind,outcome.Message);
                return new HyTcgGameplayActionResult {State=GameplayPage(v,now),Result=outcome,Wallet=Wallet(v,now)};
            });
        }
        private static HyTcgGameplayState NewGameplay(DateTime now)
        {
            return new HyTcgGameplayState {Version=2,Day=DailyKey(now),Inventory=new Dictionary<string,int>(StringComparer.Ordinal) {{"training",3},{"tea",3},{"gift",1},{"fiber",0},{"petal",0},{"crystal",0}},
                Profiles=new Dictionary<string,HyTcgCompanionProgress>(StringComparer.Ordinal),Expeditions=new List<HyTcgExpeditionRecord>(),History=new List<HyTcgGameplayEvent>()};
        }
        private static void ResetGameplay(Vault v) { v.Gameplay=null; }
        private static void EnsureGameplay(Vault v,DateTime now)
        {
            if(v.Gameplay==null)v.Gameplay=NewGameplay(now);
            var state=v.Gameplay;string day=DailyKey(now);
            // Existing protected progression keeps every balance, quota and receipt.
            // The validated version-1 inventory has only its three original supplies.
            if(state.Version==1)
            {foreach(string id in GameplayItemIds.Where(id=>!state.Inventory.ContainsKey(id)))state.Inventory.Add(id,0);state.Version=2;}
            if(state.Day!=day) {state.Day=day;state.ExpeditionStarts=0;state.FightAttempts=0;state.DateActions=0;}
        }
        private static HyTcgCompanionProgress GameplayProgress(Vault v,string cardId)
        {
            // Progress is keyed by the durable card ID, so a rarity upgrade keeps XP and
            // bond. Initialize only viewed/acted cards; a large roster does not need an
            // eager progression record or image read for every owned card.
            HyTcgCompanionProgress profile;
            if(!v.Gameplay.Profiles.TryGetValue(cardId,out profile))
            {profile=new HyTcgCompanionProgress {DateDay=v.Gameplay.Day};v.Gameplay.Profiles.Add(cardId,profile);}
            if(profile.DateDay!=v.Gameplay.Day) {profile.DateDay=v.Gameplay.Day;profile.Dates=0;}
            return profile;
        }
        private static bool IsGameplayLocked(Vault v,string cardId)
        { return v.Gameplay!=null && (v.Gameplay.Expeditions.Any(e=>!e.Claimed && e.CardIds.Contains(cardId)) || (v.Gameplay.Battle!=null && !v.Gameplay.Battle.Complete && v.Gameplay.Battle.Team.Any(u=>u.CardId==cardId))); }
        private static int GameplayLevel(int xp)
        {
            int level=1;
            while(level<GameplayMaxLevel && xp>=GameplayLevelXp(level+1))level++;
            return level;
        }
        private static int GameplayLevelXp(int level) { return 30*(level-1)*level; }
        private static int GameplayBondLevel(int bond)
        { return bond>=800?5:bond>=500?4:bond>=250?3:bond>=120?2:bond>=40?1:0; }
        private static HyTcgGameplayProfile GameplayProfile(Vault v,Owned owned)
        {
            var progress=GameplayProgress(v,owned.CardId);int level=GameplayLevel(progress.Xp),bondLevel=GameplayBondLevel(progress.Bond),tier=HyTcgRules.RarityIndex(owned.Card.Rarity);
            // Stable character variation is independent of pull ID, artwork and renderer data.
            uint seed=unchecked((uint)owned.Card.CharacterId*2654435761u);int next=level==GameplayMaxLevel?GameplayMaxXp:GameplayLevelXp(level+1);
            return new HyTcgGameplayProfile {CardId=owned.CardId,Name=owned.Card.Name,Rarity=owned.Card.Rarity,CharacterId=owned.Card.CharacterId,
                Skills=ArenaSkills(),EquippedSkills=CompanionSkills(progress,level),
                Level=level,Xp=progress.Xp,NextLevelXp=next,XpToNextLevel=Math.Max(0,next-progress.Xp),
                Effort=6+tier*6+(int)(seed%7)+(level-1)*2+bondLevel*3,
                Attack=14+tier*8+(int)((seed>>4)%10)+(level-1)*4+bondLevel*2,
                Defense=6+tier*4+(int)((seed>>9)%7)+(level-1)*2+bondLevel,
                Hp=90+tier*24+(int)((seed>>14)%21)+(level-1)*10+bondLevel*5,
                Bond=progress.Bond,BondLevel=bondLevel,Busy=IsLocked(v,owned.CardId),
                DatesRemaining=Math.Min(GameplayDateCardLimit-progress.Dates,GameplayDateDailyLimit-v.Gameplay.DateActions)};
        }
        private static List<HyTcgGameplayRoute> GameplayRoutes()
        {
            return new List<HyTcgGameplayRoute> {
                new HyTcgGameplayRoute {Id="meadow",Name="Moonlit meadow",DurationSeconds=300,MinEffort=12,RewardGems=8,RewardXp=20,RewardItems=new List<HyTcgGameplayItem> {GameplayItem("training",1),GameplayItem("fiber",2)},Description="Gather frame fiber and a training manual. Team effort adds a small gem bonus."},
                new HyTcgGameplayRoute {Id="observatory",Name="Starlight observatory",DurationSeconds=1200,MinEffort=30,RewardGems=16,RewardXp=35,RewardItems=new List<HyTcgGameplayItem> {GameplayItem("tea",1),GameplayItem("petal",2)},Description="Restore the observatory and bring home sakura petals and a tea set."},
                new HyTcgGameplayRoute {Id="ruins",Name="Ancient sky ruins",DurationSeconds=3600,MinEffort=65,RewardGems=30,RewardXp=60,RewardItems=new List<HyTcgGameplayItem> {GameplayItem("gift",1),GameplayItem("crystal",2)},Description="Recover arcane crystal and a keepsake from a demanding expedition."}
            };
        }
        private static HyTcgGameplayRoute GameplayRoute(string id)
        {
            var route=GameplayRoutes().FirstOrDefault(r=>r.Id==id);
            if(route==null)throw new HyTcgException("RouteInvalid");return route;
        }
        private static HyTcgGameplayItem GameplayItem(string id,int count)
        {
            if(id=="training")return new HyTcgGameplayItem {Id=id,Name="Training manual",Count=count,Kind="training",Description="Use on a companion to gain 80 XP."};
            if(id=="tea")return new HyTcgGameplayItem {Id=id,Name="Tea set",Count=count,Kind="date",Description="Spend a tea set during a tea date to build affinity."};
            if(id=="gift")return new HyTcgGameplayItem {Id=id,Name="Keepsake ribbon",Count=count,Kind="date",Description="Share a keepsake during a gift date to build affinity."};
            if(id=="fiber")return new HyTcgGameplayItem {Id=id,Name="Frame fiber",Count=count,Kind="material",Description="A durable weave gathered in the meadow for crafting studio frames."};
            if(id=="petal")return new HyTcgGameplayItem {Id=id,Name="Sakura petals",Count=count,Kind="material",Description="Luminous petals from the observatory for crafting studio frames."};
            if(id=="crystal")return new HyTcgGameplayItem {Id=id,Name="Arcane crystal",Count=count,Kind="material",Description="A crystal recovered from the sky ruins for crafting studio frames."};
            throw new HyTcgException("ItemInvalid");
        }
        private static HyTcgGameplayPage GameplayPage(Vault v,DateTime now)
        {
            var state=v.Gameplay;int stage=Math.Min(GameplayMaxStage,state.HighestClearedStage+1);
            int profilesTotal=v.Cards.Count(c=>!c.Dissolved && !c.Legacy);
            DateTime next=now.AddHours(7).Date.AddDays(1).AddHours(-7);
            return new HyTcgGameplayPage {Day=state.Day,ServerNow=GameplayTime(now),NextResetAt=GameplayTime(DateTime.SpecifyKind(next,DateTimeKind.Utc)),
                Battle=state.Battle==null?null:Parser().Deserialize<HyTcgArenaBattle>(Parser().Serialize(state.Battle)),
                ExpeditionStartsRemaining=GameplayExpeditionDailyLimit-state.ExpeditionStarts,ExpeditionDailyLimit=GameplayExpeditionDailyLimit,ExpeditionActiveLimit=GameplayExpeditionActiveLimit,
                DateDailyRemaining=GameplayDateDailyLimit-state.DateActions,DateDailyLimit=GameplayDateDailyLimit,DateCardLimit=GameplayDateCardLimit,
                Routes=GameplayRoutes(),Inventory=GameplayItemIds.Select(id=>GameplayItem(id,state.Inventory[id])).ToList(),
                Profiles=GameplayRosterCards(v,"").Take(500).Select(c=>GameplayProfile(v,c)).ToList(),ProfilesTotal=profilesTotal,ProfilesHasMore=profilesTotal>500,
                Expeditions=state.Expeditions.OrderByDescending(e=>e.StartedAt,StringComparer.Ordinal).Select(e=> {
                    var route=GameplayRoute(e.RouteId);
                    return new HyTcgGameplayExpedition {Id=e.Id,RouteId=e.RouteId,CardIds=new List<string>(e.CardIds),StartedAt=e.StartedAt,EndsAt=e.EndsAt,Claimed=e.Claimed,
                        Ready=!e.Claimed && now>=GameplayParseTime(e.EndsAt),Effort=e.Effort,RewardGems=e.RewardGems,RewardXp=route.RewardXp,RewardItems=route.RewardItems};
                }).ToList(),
                Fight=new HyTcgGameplayFight {Stage=stage,MaxStage=GameplayMaxStage,HighestClearedStage=state.HighestClearedStage,DailyRemaining=GameplayFightDailyLimit-state.FightAttempts,DailyLimit=GameplayFightDailyLimit,
                    EnemyName=GameplayEnemyName(stage),EnemyHp=GameplayEnemyHp(stage),EnemyAttack=GameplayEnemyAttack(stage),EnemyDefense=GameplayEnemyDefense(stage),LastResult=GameplayCopy(state.LastFight)},
                History=state.History.AsEnumerable().Reverse().Select(e=>new HyTcgGameplayEvent {Id=e.Id,At=e.At,Kind=e.Kind,Message=e.Message}).ToList()};
        }
        private static HyTcgGameplayOutcome GameplayCopy(HyTcgGameplayOutcome value)
        { return value==null?null:Parser().Deserialize<HyTcgGameplayOutcome>(Parser().Serialize(value)); }
        private static HyTcgGameplayOutcome GameplayOutcome(string kind,string title,List<string> cardIds)
        { return new HyTcgGameplayOutcome {Kind=kind,Title=title,Message="",ExpeditionId="",EnemyName="",CardIds=cardIds??new List<string>(),Items=new List<HyTcgGameplayItem>(),CombatLog=new List<string>()}; }
        private List<Owned> GameplayTeam(Vault v,Dictionary<string,object> input)
        {
            object value;
            if(!input.TryGetValue("cardIds",out value) || value==null || value is string || !(value is IEnumerable))throw new HyTcgException("TeamInvalid");
            var ids=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(object item in (IEnumerable)value)
            {
                var id=item as string;
                if(!GameplayCardId(id) || !seen.Add(id) || ids.Count>=3)throw new HyTcgException("TeamInvalid");ids.Add(id);
            }
            if(ids.Count<1)throw new HyTcgException("TeamInvalid");
            var team=new List<Owned>();
            foreach(string id in ids)
            { var card=VerifiedOwned(v,id,false);if(IsLocked(v,id))throw new HyTcgException("CardLocked");team.Add(card); }
            return team;
        }
        private Owned GameplayCompanion(Vault v,Dictionary<string,object> input)
        {
            string id=GameplayString(input,"cardId",64);if(!GameplayCardId(id))throw new HyTcgException("CardInvalid");
            var card=VerifiedOwned(v,id,false);if(IsLocked(v,id))throw new HyTcgException("CardLocked");return card;
        }
        private HyTcgGameplayOutcome GameplayStart(Vault v,DateTime now,Dictionary<string,object> input)
        {
            var route=GameplayRoute(GameplayString(input,"routeId",32));var state=v.Gameplay;
            if(state.ExpeditionStarts>=GameplayExpeditionDailyLimit)throw new HyTcgException("ExpeditionDailyLimit");
            if(state.Expeditions.Count(e=>!e.Claimed)>=GameplayExpeditionActiveLimit)throw new HyTcgException("ExpeditionActiveLimit");
            var team=GameplayTeam(v,input);int effort=team.Sum(c=>GameplayProfile(v,c).Effort);
            if(effort<route.MinEffort)throw new HyTcgException("EffortInsufficient");
            var record=new HyTcgExpeditionRecord {Id="HX-"+Guid.NewGuid().ToString("N"),RouteId=route.Id,CardIds=team.Select(c=>c.CardId).ToList(),StartedAt=GameplayTime(IsDevMode?now.AddSeconds(-route.DurationSeconds):now),EndsAt=GameplayTime(IsDevMode?now:now.AddSeconds(route.DurationSeconds)),
                Effort=effort,RewardGems=GameplayExpeditionGems(route,effort),ClaimedAt=""};
            state.Expeditions.Add(record);state.ExpeditionStarts++;
            // Keep exact active receipts and the most recent 20 completed receipts.
            var keep=new HashSet<string>(state.Expeditions.Where(e=>e.Claimed).OrderByDescending(e=>e.ClaimedAt,StringComparer.Ordinal).Take(20).Select(e=>e.Id),StringComparer.Ordinal);
            state.Expeditions.RemoveAll(e=>e.Claimed && !keep.Contains(e.Id));
            var result=GameplayOutcome("expeditionStart","Expedition started",new List<string>(record.CardIds));result.ExpeditionId=record.Id;
            result.Message=route.Name+" started with "+effort.ToString(CultureInfo.InvariantCulture)+" effort. Rewards are ready after the return time.";return result;
        }
        private static int GameplayExpeditionGems(HyTcgGameplayRoute route,int effort)
        { return route.RewardGems+Math.Min(route.RewardGems/2,effort/20); }
        private HyTcgGameplayOutcome GameplayClaim(Vault v,DateTime now,Dictionary<string,object> input)
        {
            string id=GameplayString(input,"expeditionId",64);var state=v.Gameplay;var record=state.Expeditions.FirstOrDefault(e=>e.Id==id);
            if(record==null)throw new HyTcgException("ExpeditionUnavailable");
            if(record.Claimed)throw new HyTcgException("ExpeditionClaimed");
            if(now<GameplayParseTime(record.EndsAt))throw new HyTcgException("ExpeditionNotReady");
            var route=GameplayRoute(record.RouteId);GameplayAwardGems(v,record.RewardGems);
            var items=new List<HyTcgGameplayItem>();
            foreach(var item in route.RewardItems)
            { int awarded=GameplayAddItem(state,item.Id,item.Count);if(awarded>0)items.Add(GameplayItem(item.Id,awarded)); }
            int xp=0;
            foreach(string cardId in record.CardIds)xp+=GameplayAddXp(state.Profiles[cardId],route.RewardXp);
            record.Claimed=true;record.ClaimedAt=GameplayTime(now);
            AwardActivityMission(v,now,"expedition");
            var result=GameplayOutcome("expeditionClaim","Expedition complete",new List<string>(record.CardIds));result.ExpeditionId=id;result.GemsAwarded=record.RewardGems;result.XpAwarded=xp;result.Items=items;
            result.Message=route.Name+" returned: "+record.RewardGems.ToString(CultureInfo.InvariantCulture)+" gems and "+xp.ToString(CultureInfo.InvariantCulture)+" team XP.";return result;
        }
        private HyTcgGameplayOutcome GameplayUse(Vault v,DateTime now,Dictionary<string,object> input)
        {
            string itemId=GameplayString(input,"itemId",32);GameplayItem(itemId,1);
            if(itemId=="tea" || itemId=="gift")
            {
                var dateInput=new Dictionary<string,object>(input,StringComparer.Ordinal);dateInput["activity"]=itemId;return GameplayDate(v,now,dateInput);
            }
            if(itemId!="training")throw new HyTcgException("ItemInvalid");
            var card=GameplayCompanion(v,input);var progress=GameplayProgress(v,card.CardId);
            if(progress.Xp>=GameplayMaxXp && !IsDevMode)throw new HyTcgException("LevelMaximum");
            GameplaySpendItem(v.Gameplay,itemId);
            int xp=GameplayAddXp(progress,80);var result=GameplayOutcome("inventoryUse","Training complete",new List<string> {card.CardId});result.XpAwarded=xp;
            result.Message=GameplayShortName(card.Card.Name)+" studied a training manual and gained "+xp.ToString(CultureInfo.InvariantCulture)+" XP.";return result;
        }
        private HyTcgGameplayOutcome GameplayDate(Vault v,DateTime now,Dictionary<string,object> input)
        {
            string activity=GameplayString(input,"activity",16);
            if(activity!="chat" && activity!="tea" && activity!="gift")throw new HyTcgException("DateInvalid");
            var card=GameplayCompanion(v,input);var state=v.Gameplay;var progress=GameplayProgress(v,card.CardId);
            if(state.DateActions>=GameplayDateDailyLimit || progress.Dates>=GameplayDateCardLimit)throw new HyTcgException("DateDailyLimit");
            if(progress.Bond>=GameplayMaxBond)throw new HyTcgException("BondMaximum");
            if(activity!="chat")
            {
                object supplied;if(input.TryGetValue("itemId",out supplied) && (!(supplied is string) || (string)supplied!=activity))throw new HyTcgException("ItemInvalid");
                GameplaySpendItem(state,activity);
            }
            int requested;string moment;
            using(var rng=RandomNumberGenerator.Create())
            {
                requested=activity=="chat"?12+RandomIndex(rng,7):activity=="tea"?22+RandomIndex(rng,9):40+RandomIndex(rng,16);
                string[] moments=activity=="chat"?new[] {"shared a favorite memory","talked about a future adventure","enjoyed a thoughtful conversation"}:
                    activity=="tea"?new[] {"enjoyed a peaceful afternoon tea","laughed over a warm cup of tea","shared stories beneath the stars"}:
                    new[] {"treasured the keepsake you brought","smiled at your thoughtful gift","promised to keep your ribbon safe"};
                moment=moments[RandomIndex(rng,moments.Length)];
            }
            int bond=Math.Min(requested,GameplayMaxBond-progress.Bond);progress.Bond+=bond;progress.Dates++;state.DateActions++;
            int xp=GameplayAddXp(progress,activity=="chat"?5:activity=="tea"?10:15);
            if(activity=="gift")AwardActivityMission(v,now,"gift");
            var result=GameplayOutcome("date","A moment together",new List<string> {card.CardId});result.BondAwarded=bond;result.XpAwarded=xp;
            result.Message=GameplayShortName(card.Card.Name)+" "+moment+". Affinity +"+bond.ToString(CultureInfo.InvariantCulture)+".";return result;
        }
        private HyTcgGameplayOutcome GameplayBattle(Vault v,DateTime now,Dictionary<string,object> input)
        {
            int stage=GameplayInt(input,"stage",1,GameplayMaxStage);var state=v.Gameplay;
            if(state.Battle!=null && !state.Battle.Complete)throw new HyTcgException("BattleActive");
            if(stage>Math.Min(GameplayMaxStage,state.HighestClearedStage+1))throw new HyTcgException("FightStageLocked");
            if(state.FightAttempts>=GameplayFightDailyLimit)throw new HyTcgException("FightDailyLimit");
            var profiles=GameplayTeam(v,input).Select(c=>GameplayProfile(v,c)).ToList();state.FightAttempts++;
            state.Battle=new HyTcgArenaBattle {Id="BA-"+Guid.NewGuid().ToString("N"),Stage=stage,Turn=1,Actor=0,StartedAt=GameplayTime(now),
                EnemyName=GameplayEnemyName(stage),EnemyHp=GameplayEnemyHp(stage),EnemyMaxHp=GameplayEnemyHp(stage),
                Team=profiles.Select(p=>new HyTcgArenaUnit {CardId=p.CardId,Name=p.Name,Hp=p.Hp,MaxHp=p.Hp,Attack=p.Attack,Defense=p.Defense,Energy=6,Skills=new List<string>(p.EquippedSkills)}).ToList(),
                Events=new List<HyTcgArenaEvent>(),Log=new List<string>(),LastInput=""};
            var result=GameplayOutcome("battleStart","Battle started",profiles.Select(p=>p.CardId).ToList());result.Message="Choose a skill for "+GameplayShortName(profiles[0].Name)+".";return result;
        }
        private static List<HyTcgArenaSkill> ArenaSkills()
        {
            return new List<HyTcgArenaSkill> {
                new HyTcgArenaSkill {Id="strike",Name="Strike",Kind="attack",Level=1,Energy=0,Power=100},
                new HyTcgArenaSkill {Id="guard",Name="Guard",Kind="guard",Level=1,Energy=0,Power=50},
                new HyTcgArenaSkill {Id="mend",Name="Mend",Kind="heal",Level=3,Energy=2,Power=30},
                new HyTcgArenaSkill {Id="burst",Name="Power burst",Kind="attack",Level=5,Energy=2,Power=165},
                new HyTcgArenaSkill {Id="focus",Name="Focus",Kind="focus",Level=8,Energy=1,Power=30},
                new HyTcgArenaSkill {Id="pulse",Name="Healing pulse",Kind="teamHeal",Level=12,Energy=3,Power=20},
                new HyTcgArenaSkill {Id="nova",Name="Star nova",Kind="attack",Level=18,Energy=4,Power=230}
            };
        }
        private static List<string> CompanionSkills(HyTcgCompanionProgress progress,int level)
        {return progress.Skills==null?ArenaSkills().Where(s=>s.Level<=level).Take(4).Select(s=>s.Id).ToList():new List<string>(progress.Skills);}
        private HyTcgGameplayOutcome ArenaSkillsSave(Vault v,Dictionary<string,object> input)
        {
            var card=GameplayCompanion(v,input);var progress=GameplayProgress(v,card.CardId);int level=GameplayLevel(progress.Xp);
            object raw;if(!input.TryGetValue("skillIds",out raw) || raw is string || !(raw is IEnumerable))throw new HyTcgException("SkillsInvalid");
            var ids=new List<string>();foreach(object value in (IEnumerable)raw){var id=value as string;if(id==null || ids.Count>=4 || ids.Contains(id) || !ArenaSkills().Any(s=>s.Id==id && s.Level<=level))throw new HyTcgException("SkillsInvalid");ids.Add(id);}
            if(!ids.Contains("strike"))throw new HyTcgException("SkillsInvalid");progress.Skills=ids;
            var result=GameplayOutcome("skillsSave","Skills equipped",new List<string>{card.CardId});result.Message="Skills updated for "+GameplayShortName(card.Card.Name)+".";return result;
        }
        private HyTcgGameplayOutcome ArenaTurn(Vault v,DateTime now,Dictionary<string,object> input)
        {
            var b=v.Gameplay.Battle;string id=GameplayString(input,"battleId",64),skillId=GameplayString(input,"skillId",20);
            int turn=GameplayInt(input,"turn",1,60);object supplied;string targetId=input.TryGetValue("targetId",out supplied)?supplied as string:"";
            if(targetId==null || targetId.Length>64)throw new HyTcgException("BattleInvalid");
            string intent=turn.ToString(CultureInfo.InvariantCulture)+":"+skillId+":"+targetId;
            if(b==null || b.Id!=id)throw new HyTcgException("BattleUnavailable");
            if(b.LastTurn==turn && b.LastInput==intent)return ArenaTurnOutcome(b);
            if(b.Complete)throw new HyTcgException("BattleComplete");if(turn!=b.Turn)throw new HyTcgException("BattleTurnExpired");
            var actor=b.Team[b.Actor];var skill=ArenaSkills().FirstOrDefault(s=>s.Id==skillId);
            if(skill==null || !actor.Skills.Contains(skillId) || actor.Energy<skill.Energy)throw new HyTcgException("SkillUnavailable");
            var target=targetId==""?actor:b.Team.FirstOrDefault(u=>u.CardId==targetId);
            if(skill.Kind=="heal" && (target==null || target.Hp==0))throw new HyTcgException("BattleTargetInvalid");
            if(skill.Kind!="heal" && targetId!="")throw new HyTcgException("BattleTargetInvalid");
            b.LastTurn=turn;b.LastInput=intent;b.Events.Clear();actor.Energy-=skill.Energy;
            using(var rng=RandomNumberGenerator.Create())
            {
                if(skill.Kind=="attack")
                {
                    int attack=actor.Attack*skill.Power/100;if(actor.Focus>0){attack=attack*130/100;actor.Focus--;}
                    int amount=GameplayDamage(attack,GameplayEnemyDefense(b.Stage),rng,RandomIndex(rng,100)<8);
                    b.EnemyHp=Math.Max(0,b.EnemyHp-amount);ArenaEvent(b,actor.CardId,"enemy","attack",skillId,amount,b.EnemyHp);
                }
                else if(skill.Kind=="guard"){actor.Shield=true;ArenaEvent(b,actor.CardId,actor.CardId,"guard",skillId,0,actor.Hp);}
                else if(skill.Kind=="focus"){actor.Focus=2;ArenaEvent(b,actor.CardId,actor.CardId,"focus",skillId,0,actor.Hp);}
                else foreach(var ally in skill.Kind=="teamHeal"?b.Team.Where(u=>u.Hp>0):new[]{target}.AsEnumerable())
                {int amount=Math.Min(ally.MaxHp-ally.Hp,Math.Max(1,ally.MaxHp*skill.Power/100));ally.Hp+=amount;ArenaEvent(b,actor.CardId,ally.CardId,"heal",skillId,amount,ally.Hp);}
                if(b.EnemyHp==0)return ArenaFinish(v,now,b,true);
                // A guardian occasionally heals instead of attacking; both responses are visible.
                if(b.Stage%5==0 && b.Turn%4==0 && b.EnemyHp<b.EnemyMaxHp/3)
                {int amount=Math.Min(b.EnemyMaxHp-b.EnemyHp,b.EnemyMaxHp/8);b.EnemyHp+=amount;ArenaEvent(b,"enemy","enemy","heal","mend",amount,b.EnemyHp);}
                else
                {
                    var alive=b.Team.Where(u=>u.Hp>0).ToList();var victim=alive[RandomIndex(rng,alive.Count)];
                    int hit=GameplayDamage(GameplayEnemyAttack(b.Stage),victim.Defense,rng,false);if(victim.Shield){hit=Math.Max(1,hit/2);victim.Shield=false;}
                    victim.Hp=Math.Max(0,victim.Hp-hit);ArenaEvent(b,"enemy",victim.CardId,"attack","strike",hit,victim.Hp);
                }
                if(b.Team.All(u=>u.Hp==0) || b.Turn>=60)return ArenaFinish(v,now,b,false);
                do {b.Actor=(b.Actor+1)%b.Team.Count;}while(b.Team[b.Actor].Hp==0);
                b.Team[b.Actor].Energy=Math.Min(6,b.Team[b.Actor].Energy+1);b.Turn++;
                return ArenaTurnOutcome(b);
            }
        }
        private static void ArenaEvent(HyTcgArenaBattle battle,string actor,string target,string kind,string skill,int amount,int hp)
        {
            battle.Events.Add(new HyTcgArenaEvent {Index=battle.LastTurn*10+battle.Events.Count,ActorId=actor,TargetId=target,Kind=kind,SkillId=skill,Amount=amount,HpAfter=hp});
            battle.Log.Add("Turn "+battle.Turn+": "+(actor=="enemy"?battle.EnemyName:GameplayShortName(battle.Team.First(u=>u.CardId==actor).Name))+" · "+skill+" · "+kind+" "+amount+".");
            if(battle.Log.Count>180)battle.Log.RemoveAt(0);
        }
        private static HyTcgGameplayOutcome ArenaTurnOutcome(HyTcgArenaBattle b)
        {var result=GameplayOutcome("battleTurn","Turn complete",b.Team.Select(u=>u.CardId).ToList());result.Message="Turn "+b.Turn+".";return result;}
        private HyTcgGameplayOutcome ArenaForfeit(Vault v,DateTime now,Dictionary<string,object> input)
        {
            var b=v.Gameplay.Battle;if(b==null || b.Id!=GameplayString(input,"battleId",64))throw new HyTcgException("BattleUnavailable");
            if(b.Complete)return GameplayCopy(v.Gameplay.LastFight);return ArenaFinish(v,now,b,false);
        }
        private HyTcgGameplayOutcome ArenaFinish(Vault v,DateTime now,HyTcgArenaBattle b,bool win)
        {
            b.Complete=true;b.Win=win;var state=v.Gameplay;bool first=win && b.Stage>state.HighestClearedStage;int gems=win?(first?8+b.Stage:4+b.Stage/2):0;
            GameplayAwardGems(v,gems);if(first)state.HighestClearedStage=b.Stage;if(win)AwardActivityMission(v,now,"arena");
            int xp=0;foreach(var unit in b.Team)xp+=GameplayAddXp(state.Profiles[unit.CardId],win?20+b.Stage*2:5);
            var result=GameplayOutcome("fight",win?"Victory":"Training defeat",b.Team.Select(u=>u.CardId).ToList());result.Win=win;result.Stage=b.Stage;result.Rounds=b.Turn;
            result.EnemyName=b.EnemyName;result.EnemyHp=b.EnemyHp;result.GemsAwarded=gems;result.XpAwarded=xp;result.CombatLog=new List<string>(b.Log);
            if(result.CombatLog.Count==0)result.CombatLog.Add("Battle forfeited.");
            if(win){string item=b.Stage%5==0?"gift":"training";int awarded=GameplayAddItem(state,item,1);if(awarded>0)result.Items.Add(GameplayItem(item,awarded));}
            result.Message=win?"Stage "+b.Stage+" cleared.":"Battle ended.";state.LastFight=GameplayCopy(result);return result;
        }
        private static string GameplayEnemyName(int stage) { return stage%5==0?"Sky guardian":"Training sentinel"; }
        private static int GameplayEnemyHp(int stage) { return 90+stage*55; }
        private static int GameplayEnemyAttack(int stage) { return 12+stage*5; }
        private static int GameplayEnemyDefense(int stage) { return 5+stage*3; }
        private static int GameplayDamage(int attack,int defense,RandomNumberGenerator rng,bool critical)
        { int damage=Math.Max(2,attack-defense*2/3)*(90+RandomIndex(rng,21))/100;return Math.Max(1,critical?damage*3/2:damage); }
        private static int GameplayAddXp(HyTcgCompanionProgress profile,int requested)
        { int awarded=Math.Min(requested,GameplayMaxXp-profile.Xp);profile.Xp+=awarded;return awarded; }
        private static void GameplaySpendItem(HyTcgGameplayState state,string id)
        { if(state.Inventory[id]<1)throw new HyTcgException("ItemInsufficient");state.Inventory[id]--; }
        private static int GameplayAddItem(HyTcgGameplayState state,string id,int count)
        {
            // A full stack is a normal state. Do not trap an otherwise earned expedition claim.
            int awarded=Math.Min(count,GameplayMaxItem-state.Inventory[id]);state.Inventory[id]+=awarded;return awarded;
        }
        private static void GameplayAwardGems(Vault v,int reward)
        {
            if(reward<0 || reward>100 || (long)v.Gems+RefundableGemLiability(v)>MaxGems-reward)throw new HyTcgException("GemsLimit");v.Gems+=reward;
        }
        private static void GameplayHistory(HyTcgGameplayState state,DateTime now,string kind,string message)
        {
            state.History.Add(new HyTcgGameplayEvent {Id="HE-"+Guid.NewGuid().ToString("N"),At=GameplayTime(now),Kind=kind,Message=message});
            if(state.History.Count>60)state.History.RemoveRange(0,state.History.Count-60);
        }
        private static string GameplayShortName(string name) { return name.Length>50?name.Substring(0,47)+"...":name; }
        private static string GameplayString(Dictionary<string,object> input,string key,int max)
        {
            object value;string text;
            if(!input.TryGetValue(key,out value) || (text=value as string)==null || text.Length<1 || text.Length>max)throw new HyTcgException("GameplayInvalid");return text;
        }
        private static int GameplayInt(Dictionary<string,object> input,string key,int min,int max)
        {
            object value;
            if(!input.TryGetValue(key,out value) || !(value is int))throw new HyTcgException("GameplayInvalid");
            int result=(int)value;if(result<min || result>max)throw new HyTcgException("GameplayInvalid");return result;
        }
        private static bool GameplayCardId(string id) { return Regex.IsMatch(id??"",@"^HT-\d{8}-[A-F0-9]{12}$"); }
        private static string GameplayTime(DateTime now) { return now.ToUniversalTime().ToString("o",CultureInfo.InvariantCulture); }
        private static DateTime GameplayParseTime(string value)
        {
            DateTime time;if(!DateTime.TryParseExact(value,"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out time) || time.Kind!=DateTimeKind.Utc)throw new HyTcgException("VaultInvalid");return time;
        }
        private static bool GameplayValidDay(string value)
        { DateTime day;return DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day); }
        private static void ValidateGameplay(Vault v)
        {
            var state=v.Gameplay;if(state==null)return;
            if((state.Version!=1 && state.Version!=2) || !GameplayValidDay(state.Day) || state.ExpeditionStarts<0 || state.ExpeditionStarts>GameplayExpeditionDailyLimit || state.FightAttempts<0 || state.FightAttempts>GameplayFightDailyLimit
                || state.DateActions<0 || state.DateActions>GameplayDateDailyLimit || state.HighestClearedStage<0 || state.HighestClearedStage>GameplayMaxStage
                || state.Inventory==null || state.Inventory.Count!=(state.Version==1?GameplaySupplyIds.Length:GameplayItemIds.Length) || state.Profiles==null || state.Profiles.Count>MaxCards || state.Expeditions==null || state.Expeditions.Count>23
                || state.History==null || state.History.Count>60)throw new HyTcgException("VaultInvalid");
            foreach(string item in state.Version==1?GameplaySupplyIds:GameplayItemIds)
            { int count;if(!state.Inventory.TryGetValue(item,out count) || count<0 || count>GameplayMaxItem)throw new HyTcgException("VaultInvalid"); }
            var owned=v.Cards.ToDictionary(c=>c.CardId,StringComparer.Ordinal);
            foreach(var pair in state.Profiles)
            {
                var p=pair.Value;
                if(!GameplayCardId(pair.Key) || !owned.ContainsKey(pair.Key) || p==null || p.Xp<0 || p.Xp>GameplayMaxXp || p.Bond<0 || p.Bond>GameplayMaxBond
                    || p.Dates<0 || p.Dates>GameplayDateCardLimit || !GameplayValidDay(p.DateDay)
                    || (p.Skills!=null && (p.Skills.Count<1 || p.Skills.Count>4 || !p.Skills.Contains("strike") || p.Skills.Distinct().Count()!=p.Skills.Count || p.Skills.Any(id=>!ArenaSkills().Any(s=>s.Id==id && s.Level<=GameplayLevel(p.Xp))))))throw new HyTcgException("VaultInvalid");
            }
            var ids=new HashSet<string>(StringComparer.Ordinal);var locked=new HashSet<string>(StringComparer.Ordinal);int activeCount=0;
            foreach(var e in state.Expeditions)
            {
                if(e==null || !Regex.IsMatch(e.Id??"",@"^HX-[a-f0-9]{32}$") || !ids.Add(e.Id) || e.CardIds==null || e.CardIds.Count<1 || e.CardIds.Count>3
                    || e.CardIds.Distinct(StringComparer.Ordinal).Count()!=e.CardIds.Count)throw new HyTcgException("VaultInvalid");
                HyTcgGameplayRoute route;
                try {route=GameplayRoute(e.RouteId);}catch {throw new HyTcgException("VaultInvalid");}
                DateTime start=GameplayParseTime(e.StartedAt),end=GameplayParseTime(e.EndsAt);
                if(start.Ticks>v.HighWaterTicks || (end-start).TotalSeconds!=route.DurationSeconds || e.Effort<route.MinEffort || e.Effort>400 || e.RewardGems!=GameplayExpeditionGems(route,e.Effort))throw new HyTcgException("VaultInvalid");
                if(e.Claimed)
                { DateTime claimed=GameplayParseTime(e.ClaimedAt);if(claimed<end || claimed.Ticks>v.HighWaterTicks)throw new HyTcgException("VaultInvalid"); }
                else {activeCount++;if(!String.IsNullOrEmpty(e.ClaimedAt))throw new HyTcgException("VaultInvalid");}
                foreach(string cardId in e.CardIds)
                {
                    Owned card;
                    if(!GameplayCardId(cardId) || !owned.TryGetValue(cardId,out card) || !state.Profiles.ContainsKey(cardId) || card.Legacy || (!e.Claimed && (card.Dissolved || !locked.Add(cardId))))throw new HyTcgException("VaultInvalid");
                    if(!e.Claimed && v.Upgrades.Any(u=>u.Stage!="complete" && (u.TargetId==cardId || u.MaterialIds.Contains(cardId))))throw new HyTcgException("VaultInvalid");
                }
            }
            if(activeCount>GameplayExpeditionActiveLimit)throw new HyTcgException("VaultInvalid");
            var historyIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var e in state.History)
            {
                if(e==null || !Regex.IsMatch(e.Id??"",@"^HE-[a-f0-9]{32}$") || !historyIds.Add(e.Id) || String.IsNullOrWhiteSpace(e.Message) || e.Message.Length>400
                    || (e.Kind!="expeditionStart" && e.Kind!="expeditionClaim" && e.Kind!="inventoryUse" && e.Kind!="fight" && e.Kind!="date" && e.Kind!="battleStart" && e.Kind!="skillsSave") || GameplayParseTime(e.At).Ticks>v.HighWaterTicks)throw new HyTcgException("VaultInvalid");
            }
            if(state.LastFight!=null)ValidateGameplayFight(state.LastFight,owned);
            if(state.Battle!=null)ValidateArena(v,owned,locked);
        }
        private static void ValidateArena(Vault v,Dictionary<string,Owned> owned,HashSet<string> expeditionLocks)
        {
            var b=v.Gameplay.Battle;
            if(!Regex.IsMatch(b.Id??"",@"^BA-[a-f0-9]{32}$") || b.Stage<1 || b.Stage>GameplayMaxStage || b.Turn<1 || b.Turn>60
                || b.Team==null || b.Team.Count<1 || b.Team.Count>3 || b.Team.Any(u=>u==null) || b.Team.Select(u=>u.CardId).Distinct().Count()!=b.Team.Count || b.Actor<0 || b.Actor>=b.Team.Count
                || b.EnemyName!=GameplayEnemyName(b.Stage) || b.EnemyMaxHp!=GameplayEnemyHp(b.Stage) || b.EnemyHp<0 || b.EnemyHp>b.EnemyMaxHp || b.Win!=(b.EnemyHp==0)
                || (!b.Complete && (b.EnemyHp==0 || b.Team[b.Actor].Hp==0)) || GameplayParseTime(b.StartedAt).Ticks>v.HighWaterTicks
                || b.Events==null || b.Events.Count>5 || b.Log==null || b.Log.Count>180 || b.Log.Any(s=>String.IsNullOrEmpty(s) || s.Length>200)
                || b.LastTurn<0 || b.LastTurn>b.Turn || b.LastInput==null || b.LastInput.Length>100)throw new HyTcgException("VaultInvalid");
            foreach(var u in b.Team)
            {
                Owned c;if(!owned.TryGetValue(u.CardId,out c) || c.Legacy || (!b.Complete && (c.Dissolved || expeditionLocks.Contains(u.CardId)))
                    || String.IsNullOrWhiteSpace(u.Name) || u.Name.Length>180 || u.MaxHp<1 || u.MaxHp>1000 || u.Hp<0 || u.Hp>u.MaxHp || u.Attack<1 || u.Attack>250
                    || u.Defense<1 || u.Defense>150 || u.Energy<0 || u.Energy>6 || u.Focus<0 || u.Focus>2 || u.Skills==null || u.Skills.Count>4 || !u.Skills.Contains("strike")
                    || u.Skills.Distinct().Count()!=u.Skills.Count || u.Skills.Any(id=>!ArenaSkills().Any(s=>s.Id==id)))throw new HyTcgException("VaultInvalid");
                if(!b.Complete && v.Upgrades.Any(i=>i.Stage!="complete" && (i.TargetId==u.CardId || i.MaterialIds.Contains(u.CardId))))throw new HyTcgException("VaultInvalid");
            }
            foreach(var e in b.Events)if(e==null || !new[]{"attack","heal","guard","focus"}.Contains(e.Kind) || !ArenaSkills().Any(s=>s.Id==e.SkillId)
                || (e.ActorId!="enemy" && !b.Team.Any(u=>u.CardId==e.ActorId)) || (e.TargetId!="enemy" && !b.Team.Any(u=>u.CardId==e.TargetId)) || e.Amount<0 || e.Amount>2000 || e.HpAfter<0 || e.HpAfter>2000)throw new HyTcgException("VaultInvalid");
        }
        private static void ValidateGameplayFight(HyTcgGameplayOutcome result,Dictionary<string,Owned> owned)
        {
            if(result.Kind!="fight" || result.Stage<1 || result.Stage>GameplayMaxStage || result.Rounds<1 || result.Rounds>60 || result.EnemyName!=GameplayEnemyName(result.Stage)
                || result.EnemyHp<0 || result.EnemyHp>GameplayEnemyHp(result.Stage) || result.Win!=(result.EnemyHp==0) || result.GemsAwarded<0 || result.GemsAwarded>28
                || (!result.Win && result.GemsAwarded!=0) || result.XpAwarded<0 || result.XpAwarded>180 || result.BondAwarded!=0 || String.IsNullOrWhiteSpace(result.Title) || result.Title.Length>80
                || String.IsNullOrWhiteSpace(result.Message) || result.Message.Length>400 || result.CardIds==null || result.CardIds.Count<1 || result.CardIds.Count>3
                || result.CardIds.Distinct(StringComparer.Ordinal).Count()!=result.CardIds.Count || result.CardIds.Any(id=>!owned.ContainsKey(id)) || result.Items==null || result.Items.Count>1
                || result.CombatLog==null || result.CombatLog.Count<1 || result.CombatLog.Count>180 || result.CombatLog.Any(line=>String.IsNullOrEmpty(line) || line.Length>400))throw new HyTcgException("VaultInvalid");
            foreach(var item in result.Items)
            { if(item==null || (item.Id!="training" && item.Id!="gift") || item.Count!=1 || item.Name!=GameplayItem(item.Id,1).Name || item.Kind!=GameplayItem(item.Id,1).Kind)throw new HyTcgException("VaultInvalid"); }
        }
    }
}
