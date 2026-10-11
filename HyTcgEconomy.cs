using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace HYROLauncherWeb
{
    public sealed class HyTcgWallet
    {
        public int FreeCredits { get; set; }
        public int PaidCredits { get; set; }
        public int Gems { get; set; }
        public int LimitedCredits { get; set; }
        public int LimitedPackPrice { get; set; }
        public string NextFreeAt { get; set; }
        public string ServerNow { get; set; }
        public int MaxFreeCredits { get; set; }
        public int IntervalSeconds { get; set; }
        public int PackPrice { get; set; }
        public Dictionary<string,int> SalvageValues { get; set; }
        public int PitySR { get; set; }
        public int PitySSR { get; set; }
        public int PityUR { get; set; }
        public int PitySRLimit { get; set; }
        public int PitySSRLimit { get; set; }
        public int PityURLimit { get; set; }
    }
    public sealed class HyTcgReservation
    {
        public string ReservationId { get; set; }
        public List<string> Tokens { get; set; }
        public HyTcgWallet Wallet { get; set; }
    }
    public sealed class HyTcgRecovery
    {
        public string Token { get; set; }
        public string ReservationId { get; set; }
        public HyTcgCard Card { get; set; }
    }
    public sealed class HyTcgDissolveResult
    {
        public HyTcgWallet Wallet { get; set; }
        public string FilePath { get; set; }
        public int GemsAwarded { get; set; }
    }
    public sealed class HyTcgDissolveManyResult
    {
        public HyTcgWallet Wallet { get; set; }
        public List<string> CardIds { get; set; }
        public List<string> FilePaths { get; set; }
        public int GemsAwarded { get; set; }
    }
    public sealed class HyTcgCompensationResult
    {
        public string Token { get; set; }
        public HyTcgWallet Wallet { get; set; }
        public int GemsAwarded { get; set; }
    }
    public sealed class HyTcgUpgrade
    {
        public string Token { get; set; }
        public HyTcgCard Card { get; set; }
        public List<string> MaterialIds { get; set; }
        public bool ChangeArtwork { get; set; }
        public string Stage { get; set; }
    }
    public sealed class HyTcgUpgradeResult
    {
        public HyTcgWallet Wallet { get; set; }
        public HyTcgCard Card { get; set; }
        public string FilePath { get; set; }
        public List<string> ConsumedIds { get; set; }
        public List<string> ConsumedFilePaths { get; set; }
        public string PreviousFilePath { get; set; }
    }
    internal interface IHyTcgProtector
    {
        byte[] Protect(byte[] clear);
        byte[] Unprotect(byte[] protectedBytes);
    }
    internal sealed class HyTcgUserProtector : IHyTcgProtector
    {
        private static readonly byte[] entropy=Encoding.UTF8.GetBytes("HYROLauncher.HYTCG.vault.v1");
        public byte[] Protect(byte[] clear) { return ProtectedData.Protect(clear,entropy,DataProtectionScope.CurrentUser); }
        public byte[] Unprotect(byte[] bytes) { return ProtectedData.Unprotect(bytes,entropy,DataProtectionScope.CurrentUser); }
    }

    // One protected local file is the authority for balances and card ownership. No renderer
    // values are used to award gems. DPAPI also authenticates the serialized transaction state.
    internal sealed partial class HyTcgEconomy
    {
        private sealed class Pending
        {
            public string Token { get; set; }
            public string ReservationId { get; set; }
            public string Currency { get; set; }
            public string BannerId { get; set; }
            public string Stage { get; set; }
            public HyTcgCard Card { get; set; }
            public string Fingerprint { get; set; }
        }
        private sealed class Owned
        {
            public string CardId { get; set; }
            public string FileName { get; set; }
            public string PngHash { get; set; }
            public string Fingerprint { get; set; }
            public string Signature { get; set; }
            public HyTcgCard Card { get; set; }
            public bool Legacy { get; set; }
            public bool Dissolved { get; set; }
            public bool Favorite { get; set; }
        }
        private sealed class UpgradeIntent
        {
            public string Token { get; set; }
            public string TargetId { get; set; }
            public string OriginalFingerprint { get; set; }
            public string OriginalFileName { get; set; }
            public List<string> MaterialIds { get; set; }
            public bool ChangeArtwork { get; set; }
            public string Stage { get; set; }
            public HyTcgCard Card { get; set; }
            public string Fingerprint { get; set; }
            public string CompletedFileName { get; set; }
        }
        private sealed class CompensationReceipt
        {
            public string Token { get; set; }
            public string ReservationId { get; set; }
            public int GemsAwarded { get; set; }
        }
        private sealed class Vault
        {
            public string DevSandboxBinding { get; set; }
            public int Version { get; set; }
            public int FreeCredits { get; set; }
            public int PaidCredits { get; set; }
            public int Gems { get; set; }
            public int LimitedCredits { get; set; }
            public string MissionDay { get; set; }
            public List<HyTcgMission> Missions { get; set; }
            public List<string> ResetFiles { get; set; }
            public long HighWaterTicks { get; set; }
            public long NextFreeTicks { get; set; }
            public string Key { get; set; }
            public bool MigrationComplete { get; set; }
            public List<Pending> Pending { get; set; }
            public List<Owned> Cards { get; set; }
            public int PitySR { get; set; }
            public int PitySSR { get; set; }
            public int PityUR { get; set; }
            public List<UpgradeIntent> Upgrades { get; set; }
            public List<CompensationReceipt> Compensations { get; set; }
            public HyTcgCreativeState Creative { get; set; }
            public HyTcgGameplayState Gameplay { get; set; }
        }
        private const int MaxPaid=1000000,MaxGems=1000000000,MaxCards=100000,MaxVaultBytes=64*1024*1024,MaxPngBytes=16*1024*1024;
        private static readonly object gatesLock=new object();
        private static readonly Dictionary<string,object> gates=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        private readonly object gate;
        private readonly string root,path,stampPath,mutexName;
        private readonly IHyTcgProtector protector;
        private readonly Func<DateTime> utc;
        private readonly Func<TimeSpan> elapsed;
        private readonly HashSet<string> active=new HashSet<string>(StringComparer.Ordinal);
        private long runStartTicks,lastElapsedTicks;
        private bool initialized;

        internal HyTcgEconomy(string baseDirectory) : this(baseDirectory,delegate { return DateTime.UtcNow; },new HyTcgUserProtector(),null) { }
        internal HyTcgEconomy(string baseDirectory,Func<DateTime> utcClock,IHyTcgProtector dataProtector,Func<TimeSpan> elapsedClock)
        {
            root=Path.GetFullPath(baseDirectory); path=Path.Combine(root,"hytcg-vault.dat"); stampPath=Path.Combine(root,"hytcg-vault.stamp");
            utc=utcClock; protector=dataProtector;
            var timer=Stopwatch.StartNew(); elapsed=elapsedClock??delegate { return timer.Elapsed; };
            mutexName="Local\\HYRO_HYTCG_"+Hash(Encoding.UTF8.GetBytes(root.ToUpperInvariant())).Substring(0,32);
            lock(gatesLock) { if(!gates.TryGetValue(root,out gate)) { gate=new object(); gates[root]=gate; } }
            lock(gate)
            using(var mutex=EnterMutex())
            {
                Directory.CreateDirectory(root);
                Vault state;
                if(File.Exists(path)) state=Load();
                else
                {
                    if(File.Exists(stampPath)) throw new HyTcgException("VaultInvalid");
                    byte[] key=new byte[32]; using(var rng=RandomNumberGenerator.Create()) rng.GetBytes(key);
                    long now=ClockTicks();
                    state=new Vault { Version=1,FreeCredits=HyTcgRules.MaxFreeCredits,PaidCredits=0,Gems=1800,HighWaterTicks=now,
                        NextFreeTicks=0,Key=Convert.ToBase64String(key),MigrationComplete=false,Pending=new List<Pending>(),Cards=new List<Owned>(),Upgrades=new List<UpgradeIntent>() };
                    Persist(state);
                }
                if(!File.Exists(stampPath)) WriteStamp(state);
                ValidateStamp(state);
                runStartTicks=Math.Max(ClockTicks(),state.HighWaterTicks); lastElapsedTicks=Math.Max(0,elapsed().Ticks); initialized=true;
            }
        }
        internal string VaultPath { get { return path; } }
        internal string StampPath { get { return stampPath; } }
        // The delegate reads only the native protected cache of exact character + series
        // matches. Renderer favourites or arbitrary card metadata never affect redemption.
        internal Func<HyTcgCard,int?> CharacterFavouritesLookup { get; set; }
        internal int SalvageValue(HyTcgCard card)
        {
            int value=HyTcgRules.SalvageValue(card.Rarity);
            var lookup=CharacterFavouritesLookup;
            if(lookup==null)return value;
            try
            {
                int? favourites=lookup(card);
                if(favourites.HasValue && favourites.Value>=0 && favourites.Value<=1000000000)
                    value+=favourites.Value/100;
            }
            catch { } // Unavailable enrichment retains the rarity's normal value.
            return value;
        }
        internal bool MigrationComplete { get { return Read(delegate(Vault v,DateTime now) { return v.MigrationComplete; }); } }
        internal HyTcgWallet Snapshot() { return Write(delegate(Vault v,DateTime now) { return Wallet(v,now); }); }

        internal HyTcgReservation Reserve(int count) { return Reserve(count,"credits"); }
        internal HyTcgReservation Reserve(int count,string payment) { return Reserve(count,payment,""); }
        internal HyTcgReservation Reserve(int count,string payment,string bannerId)
        {
            if(count!=1 && count!=10) throw new HyTcgException("PackInvalid");
            bool limited=payment=="limited" || payment=="limitedGems";
            if(payment!="credits" && payment!="gems" && !limited) throw new HyTcgException("PaymentInvalid");
            if(limited && !System.Text.RegularExpressions.Regex.IsMatch(bannerId??"",@"^[A-Za-z0-9_-]{1,100}$")) throw new HyTcgException("BannerUnavailable");
            return Write(delegate(Vault v,DateTime now)
            {
                if(payment=="gems" && v.Gems<HyTcgRules.PackPrice*count) throw new HyTcgException("GemsInsufficient");
                if(payment=="credits" && v.FreeCredits+v.PaidCredits<count) throw new HyTcgException("CreditsInsufficient");
                if(payment=="limitedGems" && v.Gems<HyTcgRules.LimitedPackPrice*count) throw new HyTcgException("GemsInsufficient");
                if(payment=="limited" && v.LimitedCredits<count) throw new HyTcgException("CreditsInsufficient");
                if(v.Pending.Count+count>256) throw new HyTcgException("Busy");
                string reservation="HR-"+Guid.NewGuid().ToString("N"); var tokens=new List<string>();
                for(int i=0;i<count;i++)
                {
                    string currency=limited?payment:payment=="gems"?"gems":v.FreeCredits>0?"free":"paid";
                    if(currency=="free") { v.FreeCredits--; if(v.NextFreeTicks==0) v.NextFreeTicks=now.AddSeconds(HyTcgRules.FreeIntervalSeconds).Ticks; }
                    else if(currency=="paid") v.PaidCredits--;
                    else if(currency=="limited")v.LimitedCredits--;
                    else v.Gems-=currency=="limitedGems"?HyTcgRules.LimitedPackPrice:HyTcgRules.PackPrice;
                    string token="HC-"+Guid.NewGuid().ToString("N"); tokens.Add(token);
                    v.Pending.Add(new Pending { Token=token,ReservationId=reservation,Currency=currency,Stage="reserved",BannerId=limited?bannerId:"" });
                }
                return new HyTcgReservation { ReservationId=reservation,Tokens=tokens,Wallet=Wallet(v,now) };
            });
        }
        internal void Begin(string token)
        {
            lock(gate)
            {
                if(active.Contains(token??"") || active.Contains("export:"+(token??""))) throw new HyTcgException("Busy");
                Write(delegate(Vault v,DateTime now)
                {
                    Pending p=FindPending(v,token);
                    if(p.Stage!="reserved" && p.Stage!="authorized") throw new HyTcgException("CardInvalid");
                    if(p.Card==null) p.Stage="fetching";
                    return true;
                });
                active.Add(token);
            }
        }
        internal void EndFetch(string token) { lock(gate) active.Remove(token??""); }
        internal void AuthorizeCard(string token,HyTcgCard card)
        {
            ValidateCard(card);
            HyTcgCard normalized=Write(delegate(Vault v,DateTime now)
            {
                if(active.Contains("export:"+(token??"")))throw new HyTcgException("Busy");
                Pending p=FindPending(v,token);
                if(p.Stage!="fetching" && p.Stage!="authorized") throw new HyTcgException("CardInvalid");
                if(p.Card!=null && (p.Card.CardId!=card.CardId || p.Card.CharacterId!=card.CharacterId || p.Card.Name!=card.Name
                    || p.Card.Series!=card.Series || p.Card.Gender!=card.Gender || p.Card.Rarity!=card.Rarity || p.Card.RarityColor!=card.RarityColor
                    || p.Card.CreatedAt!=card.CreatedAt)) throw new HyTcgException("CardInvalid");
                if(v.Cards.Any(c=>c.CardId==card.CardId) || v.Pending.Any(other=>other!=p && other.Card!=null && other.Card.CardId==card.CardId)) throw new HyTcgException("CardInvalid");
                var issued=Copy(card);
                if(p.Card==null)
                {
                    int tier=HyTcgRules.RarityIndex(issued.Rarity);
                    if(v.PityUR+1>=HyTcgRules.PityUR) tier=Math.Max(tier,5);
                    else if(v.PitySSR+1>=HyTcgRules.PitySSR) tier=Math.Max(tier,4);
                    else if(v.PitySR+1>=HyTcgRules.PitySR) tier=Math.Max(tier,3);
                    issued.Rarity=HyTcgService.Rarities[tier]; issued.RarityColor=HyTcgRules.RarityColor(issued.Rarity);
                    v.PitySR=tier>=3?0:v.PitySR+1;
                    v.PitySSR=tier>=4?0:v.PitySSR+1;
                    v.PityUR=tier>=5?0:v.PityUR+1;
                    SetIssuedCardLayout(issued);
                }
                p.Card=issued; p.Fingerprint=Fingerprint(issued); p.Stage="authorized";
                return issued;
            });
            // Mutate only after the durable commit, so the caller renders the exact native pity result.
            card.Rarity=normalized.Rarity; card.RarityColor=normalized.RarityColor;
            card.Metadata=normalized.Metadata;card.Orientation=normalized.Orientation;
            EndFetch(token);
        }
        internal List<HyTcgRecovery> PendingCards()
        {
            return Read(delegate(Vault v,DateTime now)
            { return v.Pending.Where(p=>p.Card!=null && p.Stage=="authorized").Select(p=>new HyTcgRecovery { Token=p.Token,ReservationId=p.ReservationId,Card=Copy(p.Card) }).ToList(); });
        }
        internal HyTcgWallet Complete(string token,HyTcgCard card,string fileName,byte[] png)
        {
            ValidateCard(card); ValidateCardBytes(card,png); SafeCardPath(fileName);
            return Write(delegate(Vault v,DateTime now)
            {
                Owned existing=v.Cards.FirstOrDefault(c=>c.CardId==card.CardId);
                if(existing!=null)
                {
                    if(existing.Dissolved || !Verify(v,existing,fileName,png) || existing.Fingerprint!=Fingerprint(card)) throw new HyTcgException("CardInvalid");
                    return Wallet(v,now);
                }
                Pending p=FindPending(v,token);
                if(p.Stage!="authorized" || p.Card==null || p.Fingerprint!=Fingerprint(card) || p.Card.CardId!=card.CardId) throw new HyTcgException("CardInvalid");
                if(v.Cards.Count>=MaxCards || v.Cards.Any(c=>String.Equals(c.FileName,fileName,StringComparison.OrdinalIgnoreCase))) throw new HyTcgException("CollectionFull");
                byte[] actual=ReadPng(fileName); if(!EqualHash(Hash(actual),Hash(png))) throw new HyTcgException("CardInvalid");
                var owned=CreateOwned(v,card,fileName,png,false); v.Cards.Add(owned); v.Pending.Remove(p);
                return Wallet(v,now);
            });
        }
        internal HyTcgWallet Refund(string token)
        {
            return Write(delegate(Vault v,DateTime now)
            {
                Pending p=v.Pending.FirstOrDefault(item=>item.Token==token);
                if(p==null) return Wallet(v,now);
                if(p.Card!=null) throw new HyTcgException("CardAlreadyIssued");
                RefundPending(v,p); active.Remove(token??""); return Wallet(v,now);
            });
        }
        // Called only for native CharacterNotFound after the bounded character attempts.
        // The consumed token and award commit together; crash recovery and duplicate
        // Completion consumes the pull and awards its compensation once.
        internal HyTcgCompensationResult CompensateCharacterMiss(string token)
        {
            lock(gate)
            {
                var result=Write(delegate(Vault v,DateTime now)
                {
                    if(v.Compensations==null)v.Compensations=new List<CompensationReceipt>();
                    var receipt=v.Compensations.FirstOrDefault(r=>r.Token==token);
                    if(receipt!=null)return new HyTcgCompensationResult {Token=token,GemsAwarded=receipt.GemsAwarded,Wallet=Wallet(v,now)};
                    Pending p=FindPending(v,token);
                    if(p.Card!=null)throw new HyTcgException("CardAlreadyIssued");
                    if(p.Stage!="fetching" || !active.Contains(token??""))throw new HyTcgException("CardInvalid");
                    if(active.Contains("export:"+(token??"")))throw new HyTcgException("Busy");
                    v.Pending.Remove(p);
                    if((long)v.Gems+RefundableGemLiability(v)>MaxGems-HyTcgRules.PackPrice)throw new HyTcgException("GemsLimit");
                    v.Gems+=HyTcgRules.PackPrice;
                    v.Compensations.Add(new CompensationReceipt {Token=token,ReservationId=p.ReservationId,GemsAwarded=HyTcgRules.PackPrice});
                    while(v.Compensations.Count>256)v.Compensations.RemoveAt(0);
                    return new HyTcgCompensationResult {Token=token,GemsAwarded=HyTcgRules.PackPrice,Wallet=Wallet(v,now)};
                });
                active.Remove(token??"");return result;
            }
        }
        internal HyTcgCompensationResult CharacterMissCompensation(string token)
        {
            return Read(delegate(Vault v,DateTime now)
            {
                var receipt=v.Compensations==null?null:v.Compensations.FirstOrDefault(r=>r.Token==token);
                return receipt==null?null:new HyTcgCompensationResult {Token=token,GemsAwarded=receipt.GemsAwarded,Wallet=Wallet(v,now)};
            });
        }
        internal HyTcgWallet CancelReservation(string reservationId)
        {
            return Write(delegate(Vault v,DateTime now)
            {
                foreach(Pending p in v.Pending.Where(p=>p.ReservationId==reservationId && p.Stage=="reserved" && p.Card==null).ToList()) RefundPending(v,p);
                return Wallet(v,now);
            });
        }
        // Call once during native startup, after cancelling any previous session. Issued cards
        // survive reopening and are returned through PendingCards instead of rerolling for free.
        internal HyTcgWallet RecoverUnused()
        {
            return Write(delegate(Vault v,DateTime now)
            {
                foreach(Pending p in v.Pending.Where(p=>p.Card==null && !active.Contains(p.Token)).ToList()) RefundPending(v,p);
                return Wallet(v,now);
            });
        }
        internal HyTcgWallet BuyCredit()
        {
            return Write(delegate(Vault v,DateTime now)
            {
                if(v.Gems<HyTcgRules.PackPrice) throw new HyTcgException("GemsInsufficient");
                if(v.PaidCredits>=MaxPaid) throw new HyTcgException("CreditsLimit");
                v.Gems-=HyTcgRules.PackPrice; v.PaidCredits++; return Wallet(v,now);
            });
        }
        internal HyTcgCard ReadCard(string fileName,byte[] png)
        {
            try
            {
                ValidateBytes(png); SafeCardPath(fileName);
                return Read(delegate(Vault v,DateTime now)
                {
                    Owned owned=v.Cards.FirstOrDefault(c=>String.Equals(c.FileName,fileName,StringComparison.OrdinalIgnoreCase));
                    return owned==null || owned.Dissolved || !Verify(v,owned,fileName,png)?null:Copy(owned.Card);
                });
            }
            catch(HyTcgException ex) { if(ex.Code=="VaultInvalid" || ex.Code=="VaultUnavailable") throw; return null; }
        }
        internal List<HyTcgCollectionItem> CollectionCards()
        {
            return Read(delegate(Vault v,DateTime now)
            {
                var result=new List<HyTcgCollectionItem>();
                foreach(Owned c in v.Cards.Where(c=>!c.Dissolved))
                {
                    try
                    {
                        byte[] png=ReadPng(c.FileName);
                        if(!Verify(v,c,c.FileName,png)) continue;
                        result.Add(new HyTcgCollectionItem { Card=Copy(c.Card),FileName=c.FileName,ImagePath=SafeCardPath(c.FileName),
                            CanDissolve=!c.Legacy && !IsLocked(v,c.CardId),SalvageValue=c.Legacy?0:SalvageValue(c.Card),IsLegacy=c.Legacy,
                            Favorite=c.Favorite,UpgradeLocked=IsLocked(v,c.CardId),CanUpgrade=!c.Legacy && !IsLocked(v,c.CardId) && HyTcgRules.RarityIndex(c.Card.Rarity)<5 });
                    }
                    catch(IOException) { }
                    catch(UnauthorizedAccessException) { }
                    catch(HyTcgException) { }
                }
                return result;
            });
        }
        internal bool CanDissolve(string cardId)
        {
            return Read(delegate(Vault v,DateTime now)
            {
                Owned c=v.Cards.FirstOrDefault(card=>card.CardId==cardId);
                if(c==null || c.Legacy || c.Dissolved || IsLocked(v,cardId)) return false;
                try { return Verify(v,c,c.FileName,ReadPng(c.FileName)); } catch { return false; }
            });
        }
        internal HyTcgDissolveResult Dissolve(string cardId)
        {
            var result=DissolveMany(new List<string> { cardId });
            return new HyTcgDissolveResult { Wallet=result.Wallet,FilePath=result.FilePaths[0],GemsAwarded=result.GemsAwarded };
        }
        internal HyTcgDissolveManyResult DissolveMany(List<string> cardIds)
        {
            ValidateIds(cardIds,100,"CardInvalid");
            return Write(delegate(Vault v,DateTime now)
            {
                var cards=new List<Owned>(); var paths=new List<string>(); int award=0;
                foreach(string id in cardIds)
                {
                    Owned c=VerifiedOwned(v,id,false);
                    if(IsLocked(v,id)) throw new HyTcgException("CardLocked");
                    int value=SalvageValue(c.Card);
                    if(value<1 || award>MaxGems-value) throw new HyTcgException("GemsLimit");
                    award+=value; cards.Add(c); paths.Add(SafeCardPath(c.FileName));
                }
                long refundableGems=RefundableGemLiability(v);
                if((long)v.Gems+refundableGems>MaxGems-award) throw new HyTcgException("GemsLimit");
                // Validate every signature/image before committing all tombstones and the one award.
                foreach(Owned card in cards) card.Dissolved=true;
                v.Gems+=award;
                return new HyTcgDissolveManyResult { Wallet=Wallet(v,now),CardIds=new List<string>(cardIds),FilePaths=paths,GemsAwarded=award };
            });
        }
        internal HyTcgCard ReadOwnedCard(string cardId)
        { return Read(delegate(Vault v,DateTime now) { return Copy(VerifiedOwned(v,cardId,true).Card); }); }
        internal List<HyTcgCard> ReadOwnedCards(List<string> cardIds)
        {
            ValidateIds(cardIds,100,"CardInvalid");
            return Read(delegate(Vault v,DateTime now)
            {
                var result=new List<HyTcgCard>();
                foreach(string id in cardIds)
                {
                    var owned=v.Cards.FirstOrDefault(c=>c.CardId==id);
                    if(owned==null || owned.Dissolved)throw new HyTcgException("CardUnavailable");
                    if(owned.Legacy)throw new HyTcgException("LegacyCard");
                    if(owned.Fingerprint!=Fingerprint(owned.Card) || !EqualHash(owned.Signature,Sign(v,owned)))throw new HyTcgException("CardInvalid");
                    result.Add(Copy(owned.Card));
                }
                return result;
            });
        }
        internal HyTcgWallet SetFavorite(string cardId,bool favorite)
        {
            return Write(delegate(Vault v,DateTime now)
            {
                // Favorite belongs to the protected local collection; it is not part of PNG identity.
                VerifiedOwned(v,cardId,true).Favorite=favorite; return Wallet(v,now);
            });
        }
        internal HyTcgUpgrade PrepareUpgrade(string cardId,List<string> materialIds,bool changeArtwork)
        {
            ValidateIds(materialIds,10,"UpgradeMaterialsInvalid");
            if(materialIds.Count!=1 && materialIds.Count!=10) throw new HyTcgException("UpgradeMaterialsInvalid");
            if(materialIds.Contains(cardId)) throw new HyTcgException("UpgradeMaterialsInvalid");
            return Write(delegate(Vault v,DateTime now)
            {
                var prior=v.Upgrades.FirstOrDefault(u=>u.TargetId==cardId && u.Stage!="complete");
                if(prior!=null)
                {
                    if(prior.ChangeArtwork==changeArtwork && prior.MaterialIds.OrderBy(s=>s,StringComparer.Ordinal).SequenceEqual(materialIds.OrderBy(s=>s,StringComparer.Ordinal))) return Upgrade(prior);
                    throw new HyTcgException("CardLocked");
                }
                if(v.Upgrades.Count(u=>u.Stage!="complete")>=32) throw new HyTcgException("Busy");
                Owned target=VerifiedOwned(v,cardId,false);
                if(IsLocked(v,cardId)) throw new HyTcgException("CardLocked");
                int next=HyTcgRules.RarityIndex(target.Card.Rarity)+1;
                if(next>=HyTcgService.Rarities.Length) throw new HyTcgException("UpgradeMax");
                foreach(string id in materialIds)
                {
                    Owned material=VerifiedOwned(v,id,false);
                    if(IsLocked(v,id)) throw new HyTcgException("CardLocked");
                    if(HyTcgRules.RarityIndex(material.Card.Rarity)!=next || (materialIds.Count==1 && material.Card.CharacterId!=target.Card.CharacterId)) throw new HyTcgException("UpgradeMaterialsInvalid");
                }
                HyTcgCard upgraded=Copy(target.Card); upgraded.Rarity=HyTcgService.Rarities[next];
                upgraded.RarityColor=HyTcgRules.RarityColor(upgraded.Rarity); upgraded.Revision++;
                SetIssuedCardLayout(upgraded);
                var intent=new UpgradeIntent { Token="HU-"+Guid.NewGuid().ToString("N"),TargetId=cardId,OriginalFingerprint=target.Fingerprint,
                    OriginalFileName=target.FileName,MaterialIds=new List<string>(materialIds),ChangeArtwork=changeArtwork,Stage="prepared",Card=upgraded,Fingerprint=Fingerprint(upgraded) };
                v.Upgrades.Add(intent); return Upgrade(intent);
            });
        }
        internal void BeginUpgrade(string token)
        {
            lock(gate)
            {
                if(active.Contains(token??"") || active.Contains("export:"+(token??""))) throw new HyTcgException("Busy");
                Read(delegate(Vault v,DateTime now)
                {
                    var intent=FindUpgrade(v,token);
                    if(intent.Stage=="complete") throw new HyTcgException("UpgradeUnavailable");
                    ValidateUpgradeParticipants(v,intent); return true;
                });
                active.Add(token);
            }
        }
        internal void EndUpgrade(string token) { EndFetch(token); }
        internal void AuthorizeUpgrade(string token,HyTcgCard card)
        {
            ValidateCard(card);
            HyTcgCard normalized=Write(delegate(Vault v,DateTime now)
            {
                if(active.Contains("export:"+(token??"")))throw new HyTcgException("Busy");
                var intent=FindUpgrade(v,token);
                if(intent.Stage=="complete") throw new HyTcgException("UpgradeUnavailable");
                ValidateUpgradeParticipants(v,intent);
                if(!SameIdentity(intent.Card,card) || intent.Card.Revision!=card.Revision) throw new HyTcgException("CardInvalid");
                if(!intent.ChangeArtwork && Fingerprint(intent.Card)!=Fingerprint(card)) throw new HyTcgException("CardInvalid");
                var issued=Copy(card);
                // The renderer may choose artwork, but export shape/layout belongs to native policy.
                SetIssuedCardLayout(issued);
                intent.Card=issued; intent.Fingerprint=Fingerprint(issued); intent.Stage="authorized"; return Copy(issued);
            });
            card.Orientation=normalized.Orientation;card.Metadata=normalized.Metadata;
            EndUpgrade(token);
        }
        internal List<HyTcgUpgrade> PendingUpgrades()
        { return Read(delegate(Vault v,DateTime now) { return v.Upgrades.Where(u=>u.Stage!="complete").Select(Upgrade).ToList(); }); }
        internal HyTcgUpgrade GetUpgrade(string token)
        { return Read(delegate(Vault v,DateTime now) { return Upgrade(FindUpgrade(v,token)); }); }
        internal HyTcgWallet CancelUpgrade(string token)
        {
            lock(gate)
            {
                if(active.Contains(token??"") || active.Contains("export:"+(token??""))) throw new HyTcgException("Busy");
                return Write(delegate(Vault v,DateTime now)
                {
                    var intent=v.Upgrades.FirstOrDefault(u=>u.Token==token);
                    if(intent==null || intent.Stage=="complete") return Wallet(v,now);
                    // Until the PNG/ledger commit no card or material is consumed, so cancel is reversible.
                    v.Upgrades.Remove(intent); return Wallet(v,now);
                });
            }
        }
        internal HyTcgUpgradeResult CompleteUpgrade(string token,HyTcgCard card,string fileName,byte[] png)
        {
            ValidateCard(card); ValidateCardBytes(card,png); SafeCardPath(fileName);
            return Write(delegate(Vault v,DateTime now)
            {
                var intent=FindUpgrade(v,token);
                if(intent.Stage=="complete")
                {
                    Owned completed=VerifiedOwned(v,intent.TargetId,false);
                    if(intent.Fingerprint!=Fingerprint(card) || !Verify(v,completed,fileName,png) || completed.Fingerprint!=intent.Fingerprint) throw new HyTcgException("CardInvalid");
                    return UpgradeResult(v,now,intent,completed);
                }
                if(intent.Stage!="authorized" || intent.Fingerprint!=Fingerprint(card)) throw new HyTcgException("CardInvalid");
                ValidateUpgradeParticipants(v,intent);
                if(String.Equals(fileName,intent.OriginalFileName,StringComparison.OrdinalIgnoreCase) || v.Cards.Any(c=>String.Equals(c.FileName,fileName,StringComparison.OrdinalIgnoreCase))) throw new HyTcgException("CardInvalid");
                if(!EqualHash(Hash(ReadPng(fileName)),Hash(png))) throw new HyTcgException("CardInvalid");
                Owned target=v.Cards.First(c=>c.CardId==intent.TargetId);
                var replacement=CreateOwned(v,card,fileName,png,false); replacement.Favorite=target.Favorite;
                foreach(string materialId in intent.MaterialIds) v.Cards.First(c=>c.CardId==materialId).Dissolved=true;
                v.Cards[v.Cards.IndexOf(target)]=replacement;
                intent.Stage="complete"; intent.CompletedFileName=fileName;
                // Retain recent commit receipts for safe save retries, without holding card locks.
                var receipts=v.Upgrades.Where(u=>u.Stage=="complete").ToList();
                while(receipts.Count>256) { v.Upgrades.Remove(receipts[0]); receipts.RemoveAt(0); }
                return UpgradeResult(v,now,intent,replacement);
            });
        }
        internal bool TryRegisterLegacy(HyTcgCard card,string fileName,byte[] png)
        {
            try
            {
                ValidateCard(card); ValidateCardBytes(card,png); SafeCardPath(fileName);
                return Write(delegate(Vault v,DateTime now)
                {
                    if(v.MigrationComplete || v.Cards.Count>=MaxCards || v.Cards.Any(c=>c.CardId==card.CardId || String.Equals(c.FileName,fileName,StringComparison.OrdinalIgnoreCase))) return false;
                    if(!EqualHash(Hash(ReadPng(fileName)),Hash(png))) return false;
                    v.Cards.Add(CreateOwned(v,card,fileName,png,true)); return true;
                });
            }
            catch(HyTcgException ex) { if(ex.Code=="VaultInvalid" || ex.Code=="VaultUnavailable") throw; return false; }
        }
        internal void FinishMigration() { Write(delegate(Vault v,DateTime now) { v.MigrationComplete=true; return true; }); }

        private static void ValidateIds(List<string> ids,int maximum,string code)
        {
            if(ids==null || ids.Count<1 || ids.Count>maximum || ids.Any(String.IsNullOrWhiteSpace)
                || ids.Distinct(StringComparer.Ordinal).Count()!=ids.Count) throw new HyTcgException(code);
        }
        private Owned VerifiedOwned(Vault v,string cardId,bool allowLegacy)
        {
            var owned=v.Cards.FirstOrDefault(c=>c.CardId==cardId);
            if(owned==null || owned.Dissolved) throw new HyTcgException("CardUnavailable");
            if(!allowLegacy && owned.Legacy) throw new HyTcgException("LegacyCard");
            if(!Verify(v,owned,owned.FileName,ReadPng(owned.FileName))) throw new HyTcgException("CardInvalid");
            return owned;
        }
        private static bool IsLocked(Vault v,string cardId)
        { return v.Upgrades.Any(u=>u.Stage!="complete" && (u.TargetId==cardId || u.MaterialIds.Contains(cardId))) || IsGameplayLocked(v,cardId); }
        private void ValidateUpgradeParticipants(Vault v,UpgradeIntent intent)
        {
            var target=VerifiedOwned(v,intent.TargetId,false);
            if(target.Fingerprint!=intent.OriginalFingerprint || target.FileName!=intent.OriginalFileName) throw new HyTcgException("CardInvalid");
            foreach(string id in intent.MaterialIds) VerifiedOwned(v,id,false);
        }
        private static UpgradeIntent FindUpgrade(Vault v,string token)
        { var intent=v.Upgrades.FirstOrDefault(u=>u.Token==token); if(intent==null) throw new HyTcgException("UpgradeUnavailable"); return intent; }
        private static HyTcgUpgrade Upgrade(UpgradeIntent intent)
        { return new HyTcgUpgrade { Token=intent.Token,Card=Copy(intent.Card),MaterialIds=new List<string>(intent.MaterialIds),ChangeArtwork=intent.ChangeArtwork,Stage=intent.Stage }; }
        private HyTcgUpgradeResult UpgradeResult(Vault v,DateTime now,UpgradeIntent intent,Owned completed)
        {
            return new HyTcgUpgradeResult { Wallet=Wallet(v,now),Card=Copy(completed.Card),FilePath=SafeCardPath(completed.FileName),
                ConsumedIds=new List<string>(intent.MaterialIds),ConsumedFilePaths=intent.MaterialIds.Select(id=>SafeCardPath(v.Cards.First(c=>c.CardId==id).FileName)).ToList(),
                PreviousFilePath=SafeCardPath(intent.OriginalFileName) };
        }
        private static bool SameIdentity(HyTcgCard a,HyTcgCard b)
        {
            return a.CardId==b.CardId && a.CharacterId==b.CharacterId && a.Name==b.Name && a.Series==b.Series && a.Gender==b.Gender
                && a.Rarity==b.Rarity && a.RarityColor==b.RarityColor && a.CreatedAt==b.CreatedAt;
        }

        private T Read<T>(Func<Vault,DateTime,T> action) { return State(action,false); }
        private T Write<T>(Func<Vault,DateTime,T> action) { return State(action,true); }
        private T State<T>(Func<Vault,DateTime,T> action,bool persist)
        {
            lock(gate)
            using(var mutex=EnterMutex())
            {
                CheckDevSession();
                Vault state=Load(); DateTime now=Advance(state);RefillDevResources(state,now);
                T result=action(state,now);
                CheckDevSession();
                if(persist) {RefillDevResources(state,now);Persist(state);}
                return result;
            }
        }
        private sealed class MutexLease : IDisposable
        {
            private readonly Mutex mutex;
            internal MutexLease(Mutex mutex) { this.mutex=mutex; }
            public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
        }
        private MutexLease EnterMutex()
        {
            var mutex=new Mutex(false,mutexName); bool acquired=false;
            try { try { acquired=mutex.WaitOne(10000); } catch(AbandonedMutexException) { acquired=true; } if(!acquired) throw new HyTcgException("Busy"); return new MutexLease(mutex); }
            catch { mutex.Dispose(); throw; }
        }
        private long ClockTicks()
        {
            DateTime now=utc(); if(now.Kind!=DateTimeKind.Utc) now=now.ToUniversalTime();
            if(now.Year<2020 || now.Year>9998) throw new HyTcgException("ClockInvalid"); return now.Ticks;
        }
        private DateTime Advance(Vault v)
        {
            long ticks=ClockTicks();
            long currentElapsed=Math.Max(0,elapsed().Ticks);
            if(initialized) ticks=Math.Max(ticks,runStartTicks+Math.Max(0,currentElapsed-lastElapsedTicks));
            ticks=Math.Max(ticks,v.HighWaterTicks); v.HighWaterTicks=ticks; var now=new DateTime(ticks,DateTimeKind.Utc);
            runStartTicks=ticks; lastElapsedTicks=currentElapsed;
            int refundableFree=v.Pending.Count(p=>p.Currency=="free" && p.Card==null);
            int capacity=HyTcgRules.MaxFreeCredits-refundableFree;
            if(v.FreeCredits>=capacity)
            {
                if(refundableFree==0 && v.FreeCredits>=HyTcgRules.MaxFreeCredits) v.NextFreeTicks=0;
                else if(v.NextFreeTicks>0 && now.Ticks>=v.NextFreeTicks) v.NextFreeTicks=now.AddSeconds(HyTcgRules.FreeIntervalSeconds).Ticks;
            }
            else if(v.NextFreeTicks==0) v.NextFreeTicks=now.AddSeconds(HyTcgRules.FreeIntervalSeconds).Ticks;
            else if(now.Ticks>=v.NextFreeTicks)
            {
                long interval=TimeSpan.FromSeconds(HyTcgRules.FreeIntervalSeconds).Ticks;
                long grant=(now.Ticks-v.NextFreeTicks)/interval+1;
                int awarded=(int)Math.Min((long)(capacity-v.FreeCredits),grant); v.FreeCredits+=awarded;
                if(v.FreeCredits>=capacity) v.NextFreeTicks=refundableFree==0?0:now.AddSeconds(HyTcgRules.FreeIntervalSeconds).Ticks;
                else v.NextFreeTicks+=grant*interval;
            }
            return now;
        }
        private static void RefundPending(Vault v,Pending p)
        {
            if(p.Card!=null) throw new HyTcgException("CardAlreadyIssued");
            if(p.Currency=="free") { if(v.FreeCredits>=HyTcgRules.MaxFreeCredits) throw new HyTcgException("VaultInvalid"); v.FreeCredits++; }
            else if(p.Currency=="paid") { if(v.PaidCredits>=MaxPaid) throw new HyTcgException("CreditsLimit"); v.PaidCredits++; }
            else if(p.Currency=="gems") { if(v.Gems>MaxGems-HyTcgRules.PackPrice) throw new HyTcgException("GemsLimit"); v.Gems+=HyTcgRules.PackPrice; }
            else if(p.Currency=="limited") { if(v.LimitedCredits>=MaxPaid)throw new HyTcgException("CreditsLimit");v.LimitedCredits++; }
            else if(p.Currency=="limitedGems") { if(v.Gems>MaxGems-HyTcgRules.LimitedPackPrice)throw new HyTcgException("GemsLimit");v.Gems+=HyTcgRules.LimitedPackPrice; }
            else throw new HyTcgException("VaultInvalid");
            v.Pending.Remove(p);
            if(v.FreeCredits==HyTcgRules.MaxFreeCredits && !v.Pending.Any(other=>other.Currency=="free" && other.Card==null)) v.NextFreeTicks=0;
        }
        private static long RefundableGemLiability(Vault v)
        { return (long)v.Pending.Count(p=>p.Currency=="gems" && p.Card==null)*HyTcgRules.PackPrice+(long)v.Pending.Count(p=>p.Currency=="limitedGems" && p.Card==null)*HyTcgRules.LimitedPackPrice; }
        private static Pending FindPending(Vault v,string token)
        { Pending p=v.Pending.FirstOrDefault(row=>row.Token==token); if(p==null) throw new HyTcgException("CardInvalid"); return p; }
        private static HyTcgWallet Wallet(Vault v,DateTime now)
        {
            return new HyTcgWallet { FreeCredits=v.FreeCredits,PaidCredits=v.PaidCredits,Gems=v.Gems,LimitedCredits=v.LimitedCredits,LimitedPackPrice=HyTcgRules.LimitedPackPrice,
                NextFreeAt=v.NextFreeTicks>0?new DateTime(v.NextFreeTicks,DateTimeKind.Utc).ToString("o",CultureInfo.InvariantCulture):"",
                ServerNow=now.ToString("o",CultureInfo.InvariantCulture),MaxFreeCredits=HyTcgRules.MaxFreeCredits,
                IntervalSeconds=HyTcgRules.FreeIntervalSeconds,PackPrice=HyTcgRules.PackPrice,
                SalvageValues=HyTcgService.Rarities.ToDictionary(r=>r,r=>HyTcgRules.SalvageValue(r)),
                PitySR=v.PitySR,PitySSR=v.PitySSR,PityUR=v.PityUR,PitySRLimit=HyTcgRules.PitySR,PitySSRLimit=HyTcgRules.PitySSR,PityURLimit=HyTcgRules.PityUR };
        }
        private Vault Load()
        {
            try
            {
                if(!File.Exists(path) || (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new HyTcgException("VaultInvalid");
                var file=new FileInfo(path); if(file.Length<16 || file.Length>MaxVaultBytes) throw new HyTcgException("VaultInvalid");
                byte[] clear=protector.Unprotect(File.ReadAllBytes(path));
                Vault v=Parser().Deserialize<Vault>(Encoding.UTF8.GetString(clear));
                // v1 installations have no progression fields; defaults are zero and an empty intent list.
                if(v!=null && v.Upgrades==null) v.Upgrades=new List<UpgradeIntent>();
                ValidateVault(v);ValidateDevVault(v);
                if(initialized || File.Exists(stampPath)) ValidateStamp(v); return v;
            }
            catch(HyTcgException) { throw; }
            catch(IOException) { throw new HyTcgException("VaultUnavailable"); }
            catch(UnauthorizedAccessException) { throw new HyTcgException("VaultUnavailable"); }
            catch { throw new HyTcgException("VaultInvalid"); }
        }
        private void Persist(Vault v)
        {
            ValidateVault(v); byte[] bytes;
            try { bytes=protector.Protect(Encoding.UTF8.GetBytes(Parser().Serialize(v))); }
            catch { throw new HyTcgException("VaultUnavailable"); }
            if(bytes.Length>MaxVaultBytes) throw new HyTcgException("CollectionFull");
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new HyTcgException("VaultInvalid");
                using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,8192,FileOptions.WriteThrough)) { file.Write(bytes,0,bytes.Length); file.Flush(true); }
                if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
            }
            catch(HyTcgException) { throw; }
            catch { throw new HyTcgException("VaultUnavailable"); }
            finally { if(File.Exists(temp)) try { File.Delete(temp); } catch { } }
        }
        private void WriteStamp(Vault v)
        {
            byte[] bytes;
            try { bytes=protector.Protect(Encoding.UTF8.GetBytes("HYTCG1:"+Hash(Convert.FromBase64String(v.Key)))); }
            catch { throw new HyTcgException("VaultUnavailable"); }
            string temp=stampPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,8192,FileOptions.WriteThrough)) { file.Write(bytes,0,bytes.Length); file.Flush(true); }
                File.Move(temp,stampPath);
            }
            catch { throw new HyTcgException("VaultUnavailable"); }
            finally { if(File.Exists(temp)) try { File.Delete(temp); } catch { } }
        }
        private void ValidateStamp(Vault v)
        {
            try
            {
                if(!File.Exists(stampPath) || (File.GetAttributes(stampPath)&FileAttributes.ReparsePoint)!=0 || new FileInfo(stampPath).Length>8192) throw new HyTcgException("VaultInvalid");
                string expected="HYTCG1:"+Hash(Convert.FromBase64String(v.Key));
                string actual=Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(stampPath)));
                if(!EqualHash(expected,actual)) throw new HyTcgException("VaultInvalid");
            }
            catch(HyTcgException) { throw; }
            catch(IOException) { throw new HyTcgException("VaultUnavailable"); }
            catch(UnauthorizedAccessException) { throw new HyTcgException("VaultUnavailable"); }
            catch { throw new HyTcgException("VaultInvalid"); }
        }
        private static void ValidateVault(Vault v)
        {
            if(v!=null && !String.IsNullOrEmpty(v.DevSandboxBinding) && !ValidHash(v.DevSandboxBinding))throw new HyTcgException("VaultInvalid");
            if(v==null || v.Version!=1 || v.FreeCredits<0 || v.FreeCredits>HyTcgRules.MaxFreeCredits || v.PaidCredits<0 || v.PaidCredits>MaxPaid || v.Gems<0 || v.Gems>MaxGems
                || v.HighWaterTicks<DateTime.Parse("2020-01-01",CultureInfo.InvariantCulture).Ticks || v.HighWaterTicks>DateTime.MaxValue.Ticks-TimeSpan.FromDays(1).Ticks
                || v.LimitedCredits<0 || v.LimitedCredits>MaxPaid || v.NextFreeTicks<0 || v.NextFreeTicks>DateTime.MaxValue.Ticks || v.Pending==null || v.Pending.Count>256 || v.Cards==null || v.Cards.Count>MaxCards
                || v.PitySR<0 || v.PitySR>=HyTcgRules.PitySR || v.PitySSR<0 || v.PitySSR>=HyTcgRules.PitySSR || v.PityUR<0 || v.PityUR>=HyTcgRules.PityUR
                || v.Upgrades==null || v.Upgrades.Count>288 || v.Upgrades.Count(u=>u.Stage!="complete")>32) throw new HyTcgException("VaultInvalid");
            byte[] key; try { key=Convert.FromBase64String(v.Key); } catch { throw new HyTcgException("VaultInvalid"); } if(key.Length!=32) throw new HyTcgException("VaultInvalid");
            var tokens=new HashSet<string>(StringComparer.Ordinal); var ids=new HashSet<string>(StringComparer.Ordinal); var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(Pending p in v.Pending)
            {
                if(p==null || String.IsNullOrWhiteSpace(p.Token) || !tokens.Add(p.Token) || String.IsNullOrWhiteSpace(p.ReservationId)
                    || (p.Currency!="free" && p.Currency!="paid" && p.Currency!="gems" && p.Currency!="limited" && p.Currency!="limitedGems") || (p.Stage!="reserved" && p.Stage!="fetching" && p.Stage!="authorized")) throw new HyTcgException("VaultInvalid");
                if((p.Currency=="limited" || p.Currency=="limitedGems") && !System.Text.RegularExpressions.Regex.IsMatch(p.BannerId??"",@"^[A-Za-z0-9_-]{1,100}$"))throw new HyTcgException("VaultInvalid");
                if(p.Stage=="authorized") { ValidateCard(p.Card); if(p.Fingerprint!=Fingerprint(p.Card) || !ids.Add(p.Card.CardId)) throw new HyTcgException("VaultInvalid"); }
                else if(p.Card!=null) throw new HyTcgException("VaultInvalid");
            }
            if(v.FreeCredits+v.Pending.Count(p=>p.Currency=="free" && p.Card==null)>HyTcgRules.MaxFreeCredits) throw new HyTcgException("VaultInvalid");
            if((long)v.Gems+(long)v.Pending.Count(p=>p.Currency=="gems" && p.Card==null)*HyTcgRules.PackPrice+(long)v.Pending.Count(p=>p.Currency=="limitedGems" && p.Card==null)*HyTcgRules.LimitedPackPrice>MaxGems
                || (long)v.LimitedCredits+v.Pending.Count(p=>p.Currency=="limited" && p.Card==null)>MaxPaid) throw new HyTcgException("VaultInvalid");
            ValidateMissions(v);
            ValidateCreative(v);
            ValidateGameplay(v);
            if(v.Compensations!=null)
            {
                if(v.Compensations.Count>256)throw new HyTcgException("VaultInvalid");
                var compensated=new HashSet<string>(StringComparer.Ordinal);
                foreach(var receipt in v.Compensations)
                    if(receipt==null || !System.Text.RegularExpressions.Regex.IsMatch(receipt.Token??"",@"^HC-[a-f0-9]{32}$") || !compensated.Add(receipt.Token)
                        || tokens.Contains(receipt.Token) || !System.Text.RegularExpressions.Regex.IsMatch(receipt.ReservationId??"",@"^HR-[a-f0-9]{32}$") || (receipt.GemsAwarded!=10 && receipt.GemsAwarded!=HyTcgRules.PackPrice))
                        throw new HyTcgException("VaultInvalid");
            }
            if(v.ResetFiles!=null && (v.ResetFiles.Count>MaxCards*3 || v.ResetFiles.Any(f=>String.IsNullOrEmpty(f) || f.Length>200 || f!=Path.GetFileName(f))))throw new HyTcgException("VaultInvalid");
            foreach(Owned c in v.Cards)
            {
                if(c==null || c.Card==null || c.CardId!=c.Card.CardId || !ids.Add(c.CardId) || String.IsNullOrWhiteSpace(c.FileName) || !files.Add(c.FileName)
                    || !ValidHash(c.PngHash) || !ValidHash(c.Fingerprint) || !ValidHash(c.Signature)) throw new HyTcgException("VaultInvalid");
                ValidateCard(c.Card);
            }
            var upgradeTokens=new HashSet<string>(StringComparer.Ordinal);
            var locked=new HashSet<string>(StringComparer.Ordinal);
            foreach(var intent in v.Upgrades)
            {
                if(intent==null || !System.Text.RegularExpressions.Regex.IsMatch(intent.Token??"",@"^HU-[a-f0-9]{32}$") || !upgradeTokens.Add(intent.Token)
                    || !ValidHash(intent.OriginalFingerprint) || String.IsNullOrWhiteSpace(intent.OriginalFileName)
                    || (intent.Stage!="prepared" && intent.Stage!="authorized" && intent.Stage!="complete") || intent.Card==null || intent.Card.CardId!=intent.TargetId
                    || !ValidHash(intent.Fingerprint) || Fingerprint(intent.Card)!=intent.Fingerprint || intent.Card.Revision<1) throw new HyTcgException("VaultInvalid");
                ValidateCard(intent.Card);
                ValidateIds(intent.MaterialIds,10,"VaultInvalid");
                if((intent.MaterialIds.Count!=1 && intent.MaterialIds.Count!=10) || intent.MaterialIds.Contains(intent.TargetId)) throw new HyTcgException("VaultInvalid");
                if(intent.Stage=="complete")
                {
                    if(String.IsNullOrWhiteSpace(intent.CompletedFileName)) throw new HyTcgException("VaultInvalid");
                    continue;
                }
                var target=v.Cards.FirstOrDefault(c=>c.CardId==intent.TargetId);
                if(target==null || target.Legacy || target.Dissolved || target.Fingerprint!=intent.OriginalFingerprint || target.FileName!=intent.OriginalFileName
                    || intent.Card.Revision!=target.Card.Revision+1 || HyTcgRules.RarityIndex(intent.Card.Rarity)!=HyTcgRules.RarityIndex(target.Card.Rarity)+1
                    || !locked.Add(intent.TargetId)) throw new HyTcgException("VaultInvalid");
                foreach(string id in intent.MaterialIds)
                {
                    var material=v.Cards.FirstOrDefault(c=>c.CardId==id);
                    if(material==null || material.Legacy || material.Dissolved || material.Card.Rarity!=intent.Card.Rarity
                        || (intent.MaterialIds.Count==1 && material.Card.CharacterId!=target.Card.CharacterId) || !locked.Add(id)) throw new HyTcgException("VaultInvalid");
                }
            }
        }
        private static JavaScriptSerializer Parser() { return new JavaScriptSerializer { MaxJsonLength=MaxVaultBytes,RecursionLimit=64 }; }
        private static HyTcgCard Copy(HyTcgCard card) { return Parser().Deserialize<HyTcgCard>(Parser().Serialize(card)); }
        private static void SetIssuedCardLayout(HyTcgCard card)
        {
            if(card.Metadata==null)card.Metadata=new Dictionary<string,string>();
            card.Orientation="portrait";
            card.Metadata["hytcg_card_layout"]=HyTcgRules.RarityIndex(card.Rarity)<=3?"window-v4":"full-v4";
        }
        private static void ValidateCard(HyTcgCard card)
        {
            if(card==null || card.CharacterId<1 || card.CharacterId>1000000 || String.IsNullOrWhiteSpace(card.Name) || card.Name.Length>180
                || !HyTcgService.Rarities.Contains(card.Rarity) || !System.Text.RegularExpressions.Regex.IsMatch(card.CardId??"",@"^HT-\d{8}-[A-F0-9]{12}$")
                || (card.Orientation!=null && card.Orientation!="" && card.Orientation!="portrait" && card.Orientation!="landscape")
                || card.ArtworkWidth<0 || card.ArtworkWidth>50000 || card.ArtworkHeight<0 || card.ArtworkHeight>50000
                || (card.ArtworkWidth==0)!=(card.ArtworkHeight==0) || card.Revision<0 || card.Revision>5) throw new HyTcgException("CardInvalid");
        }
        private static void ValidateBytes(byte[] bytes)
        {
            bytes=HyTcgStorage.ImageBytes(bytes);
            if(bytes==null || bytes.Length<24 || bytes.Length>MaxPngBytes || bytes[0]!=137 || bytes[1]!=80 || bytes[2]!=78 || bytes[3]!=71
                || !((HyTcgService.Png.Read32(bytes,16)==750 && HyTcgService.Png.Read32(bytes,20)==1050)
                    || (HyTcgService.Png.Read32(bytes,16)==1050 && HyTcgService.Png.Read32(bytes,20)==750))) throw new HyTcgException("CardInvalid");
        }
        private static void ValidateCardBytes(HyTcgCard card,byte[] bytes)
        {
            bytes=HyTcgStorage.ImageBytes(bytes);
            ValidateBytes(bytes);
            bool landscape=card.Orientation=="landscape";
            if(HyTcgService.Png.Read32(bytes,16)!=(landscape?1050:750) || HyTcgService.Png.Read32(bytes,20)!=(landscape?750:1050)) throw new HyTcgException("CardInvalid");
        }
        private string SafeCardPath(string fileName)
        {
            if(String.IsNullOrWhiteSpace(fileName) || fileName.Length>200 || fileName!=Path.GetFileName(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars())>=0
                || !(fileName.EndsWith(".png",StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".hycard",StringComparison.OrdinalIgnoreCase))
                || Path.GetFileNameWithoutExtension(fileName).EndsWith(" ",StringComparison.Ordinal)) throw new HyTcgException("CardInvalid");
            string dir=Path.Combine(root,"cards"); Directory.CreateDirectory(dir);
            if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0) throw new HyTcgException("CardInvalid");
            string full=Path.GetFullPath(Path.Combine(dir,fileName));
            if(!full.StartsWith(dir+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || (File.Exists(full) && (File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)) throw new HyTcgException("CardInvalid");
            return full;
        }
        private byte[] ReadPng(string fileName)
        {
            string full=SafeCardPath(fileName);
            if(!File.Exists(full)) throw new HyTcgException("CardUnavailable");
            var file=new FileInfo(full); if(file.Length>HyTcgStorage.MaxContainerBytes || file.Length<24) throw new HyTcgException("CardInvalid");
            byte[] bytes=File.ReadAllBytes(full);
            if(fileName.EndsWith(".hycard",StringComparison.OrdinalIgnoreCase) && !HyTcgStorage.IsContainer(bytes)) throw new HyTcgException("CardInvalid");
            ValidateBytes(bytes); return bytes;
        }
        private static Owned CreateOwned(Vault v,HyTcgCard card,string fileName,byte[] png,bool legacy)
        {
            var owned=new Owned { CardId=card.CardId,FileName=fileName,PngHash=Hash(png),Fingerprint=Fingerprint(card),Card=Copy(card),Legacy=legacy,Dissolved=false };
            owned.Signature=Sign(v,owned); return owned;
        }
        private static bool Verify(Vault v,Owned c,string fileName,byte[] png)
        {
            ValidateCardBytes(c.Card,png);
            return String.Equals(c.FileName,fileName,StringComparison.OrdinalIgnoreCase) && c.Fingerprint==Fingerprint(c.Card)
                && EqualHash(c.PngHash,Hash(png)) && EqualHash(c.Signature,Sign(v,c));
        }
        private static string Sign(Vault v,Owned c)
        {
            string data=Length(c.CardId)+Length(c.FileName)+Length(c.PngHash)+Length(c.Fingerprint)+(c.Legacy?"1":"0");
            using(var hmac=new HMACSHA256(Convert.FromBase64String(v.Key))) return Hex(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }
        private static string Fingerprint(HyTcgCard c)
        {
            var b=new StringBuilder();
            foreach(string value in new[] { c.CharacterId.ToString(CultureInfo.InvariantCulture),c.Name,c.Series,c.Gender,c.Rarity,c.RarityColor,c.SourceImage,c.ProfileImage,
                c.ImageSource,c.ArtworkPage,c.OriginalSource,c.FallbackReason,c.CreatedAt,c.CardId }) b.Append(Length(value));
            if(c.Metadata!=null) foreach(var pair in c.Metadata.OrderBy(p=>p.Key,StringComparer.Ordinal)) { b.Append(Length(pair.Key)); b.Append(Length(pair.Value)); }
            // Optional extension: old portrait models with no source dimensions/revision retain
            // byte-for-byte v1 canonical identity and their existing signatures remain valid.
            if(c.Orientation=="landscape" || c.ArtworkWidth!=0 || c.ArtworkHeight!=0 || c.Revision!=0)
            {
                b.Append(Length("HYTCG2")); b.Append(Length(c.Orientation=="landscape"?"landscape":"portrait"));
                b.Append(Length(c.ArtworkWidth.ToString(CultureInfo.InvariantCulture))); b.Append(Length(c.ArtworkHeight.ToString(CultureInfo.InvariantCulture)));
                b.Append(Length(c.Revision.ToString(CultureInfo.InvariantCulture)));
            }
            return Hash(Encoding.UTF8.GetBytes(b.ToString()));
        }
        private static string Length(string text) { text=text??""; return text.Length.ToString(CultureInfo.InvariantCulture)+":"+text; }
        private static string Hash(byte[] bytes) { using(var sha=SHA256.Create()) return Hex(sha.ComputeHash(bytes)); }
        private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-",""); }
        private static bool ValidHash(string text) { return System.Text.RegularExpressions.Regex.IsMatch(text??"",@"^[A-F0-9]{64}$"); }
        private static bool EqualHash(string a,string b)
        { if(a==null || b==null || a.Length!=b.Length) return false; int diff=0; for(int i=0;i<a.Length;i++) diff|=a[i]^b[i]; return diff==0; }
    }
}
