using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HYROLauncherWeb
{
    internal sealed partial class HyTcgEconomy
    {
        // This flag belongs only to a native instance produced by a sandbox clone.
        // It is deliberately absent from the vault and every renderer input model.
        private bool devMode,devClosed;
        internal bool IsDevMode { get { return devMode; } }
        internal Func<bool> DevSessionAlive { get; set; }

        internal void CloseDevMode()
        { lock(gate) { if(devMode)devClosed=true; } }

        private void CheckDevSession()
        {
            if(!devMode)return;
            var alive=DevSessionAlive;
            if(devClosed || (alive!=null && !alive()))throw new HyTcgException("DevExpired");
        }

        private void ValidateDevVault(Vault v)
        {
            if(String.IsNullOrEmpty(v.DevSandboxBinding))return;
            // Even a byte-for-byte copy of this protected disposable wallet must
            // never load as real progress or be mounted at another account root.
            if(!devMode || !EqualHash(v.DevSandboxBinding,Hash(Encoding.UTF8.GetBytes(root.ToUpperInvariant()))))throw new HyTcgException("VaultInvalid");
        }

        // Copy under the same native mutex used by account transactions. Loading
        // here does not advance clocks, recover a reservation, or write real state.
        internal HyTcgEconomy CreateDevSandbox(string sandboxDirectory)
        {
            string destination=Path.GetFullPath(sandboxDirectory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            string source=root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(devMode || String.Equals(source,destination,StringComparison.OrdinalIgnoreCase)
                || source.StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
                || destination.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new HyTcgException("DevSandboxInvalid");
            DevCheckPath(destination);
            lock(gate)
            using(var mutex=EnterMutex())
            {
                if(active.Count>0)throw new HyTcgException("Busy");
                var state=Load();
                Directory.CreateDirectory(destination);
                if(Directory.EnumerateFileSystemEntries(destination).Any())throw new HyTcgException("DevSandboxInvalid");
                File.Copy(path,Path.Combine(destination,"hytcg-vault.dat"),false);
                File.Copy(stampPath,Path.Combine(destination,"hytcg-vault.stamp"),false);
                DevCopyFiles(Path.Combine(root,"cards"),Path.Combine(destination,"cards"),state.Cards.Where(c=>!c.Dissolved).Select(c=>c.FileName));
                if(state.Creative!=null)
                    DevCopyFiles(Path.Combine(root,"hytcg-artworks"),Path.Combine(destination,"hytcg-artworks"),state.Creative.Artworks.Select(a=>a.Id+".dat"));
                // Thumbnail cache is derived local data; copying it avoids decrypting
                // the whole collection again when entering this short-lived session.
                DevCopyDirectory(Path.Combine(root,"hytcg-previews"),Path.Combine(destination,"hytcg-previews"));
            }
            var clone=new HyTcgEconomy(destination,utc,protector,null);
            clone.devMode=true;
            clone.Write(delegate(Vault v,DateTime now) {
                v.Pending.Clear();v.Upgrades.Clear();v.ResetFiles=null;v.Compensations=null;
                EnsureGameplay(v,now);v.Gameplay.Battle=null;v.Gameplay.Expeditions.Clear();
                return true;
            });
            return clone;
        }

        private static void DevCheckPath(string path)
        {
            for(var directory=new DirectoryInfo(path);directory!=null;directory=directory.Parent)
                if(directory.Exists && (directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("DevSandboxInvalid");
        }
        private static void DevCopyFiles(string from,string to,IEnumerable<string> fileNames)
        {
            DevCheckPath(from);DevCheckPath(to);Directory.CreateDirectory(to);
            foreach(string name in fileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if(String.IsNullOrWhiteSpace(name) || Path.GetFileName(name)!=name)throw new HyTcgException("DevSandboxInvalid");
                string file=Path.Combine(from,name);
                if(!File.Exists(file))continue;
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("DevSandboxInvalid");
                File.Copy(file,Path.Combine(to,name),false);
            }
        }
        private static void DevCopyDirectory(string from,string to)
        {
            if(!Directory.Exists(from))return;DevCheckPath(from);
            DevCopyFiles(from,to,Directory.EnumerateFiles(from).Select(Path.GetFileName));
        }

        private void RefillDevResources(Vault v,DateTime now)
        {
            if(!devMode)return;CheckDevSession();
            v.DevSandboxBinding=Hash(Encoding.UTF8.GetBytes(root.ToUpperInvariant()));
            // Headroom preserves all normal overflow and reservation validation.
            // Infinity is presented by the UI; these native reserves refill on every
            // transaction instead of weakening protected account constraints.
            v.Gems=100000000;v.PaidCredits=100000;
            v.LimitedCredits=100000;
            v.FreeCredits=HyTcgRules.MaxFreeCredits-v.Pending.Count(p=>p.Currency=="free" && p.Card==null);
            if(!v.Pending.Any(p=>p.Currency=="free" && p.Card==null))v.NextFreeTicks=0;
            EnsureGameplay(v,now);var gameplay=v.Gameplay;
            gameplay.ExpeditionStarts=0;gameplay.FightAttempts=0;gameplay.DateActions=0;gameplay.HighestClearedStage=GameplayMaxStage;
            foreach(string id in GameplayItemIds)gameplay.Inventory[id]=900;
            foreach(var card in v.Cards.Where(c=>!c.Dissolved && !c.Legacy))
            {
                var profile=GameplayProgress(v,card.CardId);profile.Xp=GameplayMaxXp;profile.Dates=0;
                // Keep gifts testable indefinitely while unlocking every bond tier.
                profile.Bond=800;
            }
            var creative=EnsureCreative(v);
            for(int i=0;i<64;i++)foreach(int roll in new[]{0,1})
            {
                var dye=RollFrameDye(i,roll);creative.Dyes[dye.Id]=500;
            }
        }

        // Supply a real, native-signed material in the temporary collection so the
        // existing upgrade picker and validation need no alternative dev bypass.
        internal string EnsureDevUpgradeMaterial(string cardId)
        {
            if(!devMode)throw new HyTcgException("DevUnavailable");
            return Write(delegate(Vault v,DateTime now) {
                var target=VerifiedOwned(v,cardId,false);int next=HyTcgRules.RarityIndex(target.Card.Rarity)+1;
                if(next>=HyTcgService.Rarities.Length)return "";
                var existing=v.Cards.FirstOrDefault(c=>!c.Dissolved && !c.Legacy && c.CardId!=cardId
                    && c.Card.CharacterId==target.Card.CharacterId && c.Card.Rarity==HyTcgService.Rarities[next] && !IsLocked(v,c.CardId));
                if(existing!=null)return existing.CardId;
                if(v.Cards.Count>=MaxCards)throw new HyTcgException("CollectionFull");
                var material=Copy(target.Card);material.CardId="HT-"+now.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant();
                material.CreatedAt=now.ToString("o",CultureInfo.InvariantCulture);material.Rarity=HyTcgService.Rarities[next];material.RarityColor=HyTcgRules.RarityColor(material.Rarity);material.Revision=0;
                SetIssuedCardLayout(material);
                byte[] source=HyTcgStorage.ImageBytes(ReadPng(target.FileName));
                string rendered=HyTcgCosmeticRenderer.RenderDevMaterial(target.Card,material,"data:image/png;base64,"+Convert.ToBase64String(source));
                source=Convert.FromBase64String(rendered.Substring(rendered.IndexOf(',')+1));
                CheckDevSession();
                byte[] bytes=HyTcgStorage.Protect(HyTcgService.Png.WithMetadata(source,Parser().Serialize(material)));
                string name="DEV_"+material.CardId+".hycard";File.WriteAllBytes(SafeCardPath(name),bytes);
                v.Cards.Add(CreateOwned(v,material,name,bytes,false));return material.CardId;
            });
        }
    }
}
