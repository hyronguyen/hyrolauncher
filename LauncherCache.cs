using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HYROLauncherWeb
{
    internal sealed class LauncherCacheCategory
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public long Bytes { get; set; }
        public string Size { get; set; }
        public int Files { get; set; }
    }
    internal sealed class LauncherCacheCleanup
    {
        public long FreedBytes { get; set; }
        public string FreedSize { get; set; }
        public int DeletedFiles { get; set; }
        public int SkippedFiles { get; set; }
    }
    internal sealed class LauncherDirectoryUsage
    {
        internal long Bytes;
        internal int Files;
    }
    internal sealed class LauncherStorageSnapshot
    {
        internal long OwnedBytes,WebBytes,AssetBytes,LibraryBytes,PluginBytes,BuiltInBytes,CardBytes;
        internal int AssetFiles;
        internal List<LauncherCacheCategory> Categories;
    }
    // This allowlist contains only files which the launcher can regenerate. In
    // particular, .cache is not a disposable directory: it also contains artwork
    // and banner history. Cookies, site storage and the card vault are never targets.
    internal static class LauncherCache
    {
        private sealed class Area
        {
            internal string Id, Name, Root, Directory, Pattern;
            internal bool Recursive;
        }
        private static readonly object maintenanceGate = new object();
        private static readonly Dictionary<string, DateTime> nextMaintenance = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string,int> maintenanceWrites = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        private const string IgdbPattern = @"^(?:[1-9][0-9]*|steam-[1-9][0-9]*)\.json$";
        private const string ExplorePattern = @"^[a-fA-F0-9]{64}\.json$";
        internal static string FormatBytes(long bytes)
        {
            double value = Math.Max(0, bytes); string[] units = { "B", "KB", "MB", "GB", "TB" }; int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return value.ToString(unit == 0 ? "0" : "0.##", CultureInfo.InvariantCulture) + " " + units[unit];
        }
        internal static bool SafePath(string trustedRoot, string target)
        {
            try
            {
                string root = Path.GetFullPath(trustedRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string full = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!String.Equals(root, full, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
                string cursor = full;
                while (true)
                {
                    if ((File.Exists(cursor) || Directory.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0) return false;
                    if (String.Equals(cursor, root, StringComparison.OrdinalIgnoreCase)) return true;
                    cursor = Path.GetDirectoryName(cursor); if (String.IsNullOrEmpty(cursor)) return false;
                }
            }
            catch { return false; }
        }
        private static List<FileInfo> Files(string root, string directory, bool recursive, string pattern)
        {
            var result = new List<FileInfo>();
            if (!Directory.Exists(directory) || !SafePath(root, directory)) return result;
            var queue = new Queue<string>(); queue.Enqueue(directory);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue(); if (!SafePath(root, current)) continue;
                try
                {
                    foreach (string path in Directory.GetFiles(current))
                        try { if (SafePath(root, path) && (pattern == null || Regex.IsMatch(Path.GetFileName(path), pattern, RegexOptions.IgnoreCase))) result.Add(new FileInfo(path)); } catch { }
                    if (recursive) foreach (string child in Directory.GetDirectories(current)) if (SafePath(root, child)) queue.Enqueue(child);
                }
                catch { }
            }
            return result;
        }
        internal static LauncherDirectoryUsage Usage(string root, string directory)
        {
            var result = new LauncherDirectoryUsage();
            foreach (var file in Files(root, directory, true, null)) try { result.Bytes += file.Length; result.Files++; } catch { }
            return result;
        }
        internal static LauncherStorageSnapshot ReadStorage(string root,string webRoot)
        {
            var result=new LauncherStorageSnapshot(); var areas=Areas(root,webRoot);
            result.Categories=areas.GroupBy(area=>area.Id).Select(group=>new LauncherCacheCategory {Id=group.Key,Name=group.First().Name}).ToList();
            string assets=Path.Combine(root,"assets"),library=Path.Combine(assets,"library"),plugins=Path.Combine(assets,"plugins"),builtin=Path.Combine(assets,"builtin-plugins"),cards=Path.Combine(root,"cards");
            Action<FileInfo,bool> record=delegate(FileInfo file,bool web)
            {
                try
                {
                    long bytes=file.Length;
                    if(web)result.WebBytes+=bytes;
                    else
                    {
                        result.OwnedBytes+=bytes;
                        if(Within(assets,file.FullName)){result.AssetBytes+=bytes;result.AssetFiles++;}
                        if(Within(library,file.FullName))result.LibraryBytes+=bytes;
                        if(Within(plugins,file.FullName))result.PluginBytes+=bytes;
                        if(Within(builtin,file.FullName))result.BuiltInBytes+=bytes;
                        if(Within(cards,file.FullName))result.CardBytes+=bytes;
                    }
                    foreach(var area in areas)
                        if (Within(area.Directory,file.FullName) && (area.Recursive || String.Equals(Path.GetDirectoryName(file.FullName),Path.GetFullPath(area.Directory).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) &&
                            (area.Pattern==null || Regex.IsMatch(file.Name,area.Pattern,RegexOptions.IgnoreCase)))
                        {var category=result.Categories.First(item=>item.Id==area.Id);category.Bytes+=bytes;category.Files++;break;}
                }
                catch { }
            };
            bool nestedWeb=!String.IsNullOrWhiteSpace(webRoot) && (Within(root,webRoot) || String.Equals(Path.GetFullPath(root).TrimEnd('\\','/'),Path.GetFullPath(webRoot).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase));
            foreach(var file in Files(root,root,true,null))record(file,nestedWeb && Within(webRoot,file.FullName));
            if(!String.IsNullOrWhiteSpace(webRoot) && !nestedWeb)foreach(var file in Files(webRoot,webRoot,true,null))record(file,true);
            foreach(var category in result.Categories)category.Size=FormatBytes(category.Bytes);
            return result;
        }
        private static bool Within(string directory,string path)
        {return Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory).TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
        private static List<Area> Areas(string root, string webRoot)
        {
            var areas = new List<Area> {
                new Area { Id="igdb", Name="IGDB metadata", Root=root, Directory=Path.Combine(root,"igdb-cache"), Pattern=IgdbPattern },
                new Area { Id="explore", Name="Explore & Steam Store", Root=root, Directory=Path.Combine(root,"explore-cache"), Pattern=ExplorePattern },
                new Area { Id="backgrounds", Name="Steam background metadata", Root=root, Directory=Path.Combine(root,"background-cache"), Pattern=@"^[0-9]{17}\.json$" },
                new Area { Id="previews", Name="Card previews", Root=root, Directory=Path.Combine(root,"hytcg-previews"), Pattern=@"^[a-fA-F0-9]{64}\.hypreview$" },
                new Area { Id="pet", Name="Pet animation variants", Root=root, Directory=Path.Combine(root,"cache","pet-timing"), Pattern=@"^[a-fA-F0-9]{64}-[0-9]{2,4}\.gif$" }
            };
            // Normal WebView2 data is nested under EBWebView in current runtimes.
            // Profiles and runtime caches are discovered only at these known depths.
            if (!String.IsNullOrWhiteSpace(webRoot) && Directory.Exists(webRoot) && SafePath(webRoot, webRoot))
            {
                var containers = new List<string> { webRoot };
                string edge = Path.Combine(webRoot, "EBWebView"); if (Directory.Exists(edge) && SafePath(webRoot, edge)) containers.Add(edge);
                var profiles = new List<string>(containers);
                foreach (string container in containers)
                    try { foreach (string dir in Directory.GetDirectories(container)) { string name=Path.GetFileName(dir); if (SafePath(webRoot,dir) && (name=="Default" || name.StartsWith("Profile ",StringComparison.OrdinalIgnoreCase) || name.StartsWith("HYRO",StringComparison.OrdinalIgnoreCase))) profiles.Add(dir); } } catch { }
                string[] names = { "Cache", "Code Cache", "GPUCache", "DawnCache", "ShaderCache", "GrShaderCache", "GraphiteDawnCache", "Network\\Cache" };
                foreach (string profile in profiles.Distinct(StringComparer.OrdinalIgnoreCase)) foreach (string name in names)
                    areas.Add(new Area { Id="web", Name="WebView image & code cache", Root=webRoot, Directory=Path.Combine(profile,name), Recursive=true });
            }
            return areas;
        }
        internal static List<LauncherCacheCategory> Inspect(string root, string webRoot)
        {
            var result = new List<LauncherCacheCategory>();
            foreach (var group in Areas(root,webRoot).GroupBy(area=>area.Id))
            {
                long bytes=0; int count=0;
                foreach (var area in group) foreach (var file in Files(area.Root,area.Directory,area.Recursive,area.Pattern)) try { bytes+=file.Length; count++; } catch { }
                result.Add(new LauncherCacheCategory { Id=group.Key, Name=group.First().Name, Bytes=bytes, Files=count, Size=FormatBytes(bytes) });
            }
            return result;
        }
        internal static LauncherCacheCleanup Clean(string root, string webRoot)
        {
            var result = new LauncherCacheCleanup();
            foreach (var area in Areas(root,webRoot)) foreach (var file in Files(area.Root,area.Directory,area.Recursive,area.Pattern))
            {
                try
                {
                    // Check the final path again immediately before deleting; never
                    // recursively remove a cache directory or follow linked entries.
                    if (!SafePath(area.Root,file.FullName)) { result.SkippedFiles++; continue; }
                    long bytes=file.Length; File.Delete(file.FullName); result.FreedBytes+=bytes; result.DeletedFiles++;
                }
                catch { result.SkippedFiles++; }
            }
            result.FreedSize=FormatBytes(result.FreedBytes); return result;
        }
        internal static bool DeleteUnusedAssetDirectory(string root,string target)
        {
            if(!SafePath(root,target) || !String.Equals(Path.GetDirectoryName(Path.GetFullPath(target)),Path.GetFullPath(root).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))return false;
            return DeleteDirectoryTree(root,target);
        }
        internal static bool DeleteDirectoryTree(string root,string target)
        {
            if(!SafePath(root,target) || !Within(root,target))return false;
            var directories=new List<string>();var queue=new Queue<string>();queue.Enqueue(target);
            while(queue.Count>0)
            {
                string dir=queue.Dequeue();if(!SafePath(root,dir))continue;directories.Add(dir);
                try {foreach(string child in Directory.GetDirectories(dir))if(SafePath(root,child))queue.Enqueue(child);}catch { }
            }
            foreach(var file in Files(root,target,true,null))try {if(SafePath(root,file.FullName))File.Delete(file.FullName);}catch { }
            foreach(string dir in directories.AsEnumerable().Reverse())try {if(SafePath(root,dir))Directory.Delete(dir,false);}catch { }
            return !Directory.Exists(target);
        }
        internal static string ReadText(string root, string path, int maximumBytes)
        {
            try
            {
                if (!SafePath(root,path) || !File.Exists(path) || new FileInfo(path).Length>maximumBytes) return null;
                string text=File.ReadAllText(path,Encoding.UTF8); Touch(root,path); return text;
            }
            catch { return null; }
        }
        internal static void Touch(string root,string path)
        {
            try { if (SafePath(root,path) && File.Exists(path) && File.GetLastAccessTimeUtc(path)<DateTime.UtcNow.AddHours(-1)) File.SetLastAccessTimeUtc(path,DateTime.UtcNow); } catch { }
        }
        internal static bool WriteText(string root,string path,string text)
        {
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                if (!SafePath(root,path)) return false; Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!SafePath(root,path)) return false;
                File.WriteAllText(temporary,text,Encoding.UTF8);
                if (File.Exists(path)) { if(!SafePath(root,path)) return false; File.Replace(temporary,path,null); } else File.Move(temporary,path);
                Touch(root,path); Maintain(root,Path.GetDirectoryName(path),false); return true;
            }
            catch { return false; }
            finally { try { if (File.Exists(temporary) && SafePath(root,temporary)) File.Delete(temporary); } catch { } }
        }
        // Maintenance is lazy, once per hour after an actual cache write. No timer
        // runs when idle. Last-access ordering retains frequently used metadata;
        // the API-specific freshness TTLs remain in their respective services.
        internal static void Maintain(string root,string directory,bool force)
        {
            string name=Path.GetFileName(directory); string pattern; int maxFiles,maxDays; long maxBytes;
            if(name=="igdb-cache") { pattern=IgdbPattern; maxFiles=2000; maxDays=60; maxBytes=64L*1024*1024; }
            else if(name=="explore-cache") { pattern=ExplorePattern; maxFiles=300; maxDays=7; maxBytes=32L*1024*1024; }
            else if(name=="background-cache") { pattern=@"^[0-9]{17}\.json$"; maxFiles=64; maxDays=30; maxBytes=16L*1024*1024; }
            else if(name=="pet-timing") { pattern=@"^[a-fA-F0-9]{64}-[0-9]{2,4}\.gif$"; maxFiles=64; maxDays=30; maxBytes=128L*1024*1024; }
            else return;
            if (!SafePath(root,directory)) return;
            lock(maintenanceGate)
            {
                int writes;maintenanceWrites.TryGetValue(directory,out writes);maintenanceWrites[directory]=writes+1;
                DateTime next; if(!force && writes<31 && nextMaintenance.TryGetValue(directory,out next) && DateTime.UtcNow<next) return;
                if(nextMaintenance.Count>=128 && !nextMaintenance.ContainsKey(directory)){string old=nextMaintenance.Keys.First();nextMaintenance.Remove(old);maintenanceWrites.Remove(old);}
                nextMaintenance[directory]=DateTime.UtcNow.AddHours(1);
                maintenanceWrites[directory]=0;
                var files=Files(root,directory,false,pattern).OrderByDescending(file=>Recent(file)).ToList(); long total=0; int count=0;
                foreach(var file in files)
                {
                    try
                    {
                        long bytes=file.Length;
                        if (Recent(file)<DateTime.UtcNow.AddDays(-maxDays) || count>=maxFiles || total+bytes>maxBytes) { if(SafePath(root,file.FullName))File.Delete(file.FullName); }
                        else { total+=bytes; count++; }
                    }
                    catch { }
                }
                // Abandoned atomic writes are safe to discard only after a day.
                foreach(var file in Files(root,directory,false,@"\.json\.[a-fA-F0-9]{32}\.tmp$"))
                    try { if(file.LastWriteTimeUtc<DateTime.UtcNow.AddDays(-1) && SafePath(root,file.FullName))File.Delete(file.FullName); } catch { }
            }
        }
        private static DateTime Recent(FileInfo file) { return file.LastAccessTimeUtc>file.LastWriteTimeUtc?file.LastAccessTimeUtc:file.LastWriteTimeUtc; }
    }
}
