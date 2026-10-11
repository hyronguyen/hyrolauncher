using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HYROLauncherWeb
{
    internal static class BookshelfArtworkLighting
    {
        private static readonly object CacheGate = new object();
        private static readonly Dictionary<string, string> Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static string ReadColor(string artworkPath)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(artworkPath)) return "";
                string path = Path.GetFullPath(artworkPath);
                // Ambient lighting never accesses network shares or downloads artwork.
                if (path.StartsWith(@"\\", StringComparison.Ordinal)) return "";
                FileInfo file = new FileInfo(path);
                if (!file.Exists || file.Length <= 0 || file.Length > 16L * 1024 * 1024) return "";
                string key = path + "|" + file.LastWriteTimeUtc.Ticks + "|" + file.Length;
                string cached;
                lock (CacheGate) if (Colors.TryGetValue(key, out cached)) return cached;
                string color = "";
                using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (Image source = Image.FromStream(input, false, false))
                {
                    if (source.Width > 8192 || source.Height > 8192 || (long)source.Width * source.Height > 16000000) return "";
                    using (Bitmap sample = new Bitmap(8, 8))
                    {
                        using (Graphics graphics = Graphics.FromImage(sample))
                        {
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            graphics.DrawImage(source, new Rectangle(0, 0, 8, 8));
                        }
                        double red = 0, green = 0, blue = 0, total = 0;
                        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                        {
                            Color pixel = sample.GetPixel(x, y);
                            int high = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                            int low = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                            double weight = (high - low + 12) * pixel.A / 255.0;
                            red += pixel.R * weight; green += pixel.G * weight; blue += pixel.B * weight; total += weight;
                        }
                        if (total > 0) color = Channel(red / total) + " " + Channel(green / total) + " " + Channel(blue / total);
                    }
                }
                lock (CacheGate)
                {
                    if (Colors.Count >= 256) Colors.Clear();
                    Colors[key] = color;
                }
                return color;
            }
            catch { return ""; }
        }

        private static int Channel(double value) { return Math.Max(35, Math.Min(255, (int)Math.Round(value))); }
    }

    internal partial class WebLauncherForm
    {
        private readonly SemaphoreSlim bookshelfArtworkWorkers = new SemaphoreSlim(2, 2);
        private readonly HashSet<string> bookshelfArtworkPending = new HashSet<string>(StringComparer.Ordinal);

        private string BookshelfArtworkPath(string gameId)
        {
            if (state == null || state.Games == null || String.IsNullOrWhiteSpace(gameId)) return "";
            GameEntry game = state.Games.Find(candidate => candidate != null && candidate.Id == gameId);
            if (game != null)
            {
                // A color request cannot reveal private artwork, even with a forged renderer request.
                if (game.Private) return "";
                string path = String.IsNullOrWhiteSpace(game.IconPath) ? game.BannerPath : game.IconPath;
                return IsRemoteUrl(path) ? "" : path;
            }
            if (steamLibrary == null || steamLibrary.Games == null || state.Settings == null || steamLibrary.SteamId != state.Settings.SteamId) return "";
            foreach (SteamGame steam in steamLibrary.Games)
            {
                if (steam != null && gameId == "steam-" + steamLibrary.SteamId + "-" + steam.AppId)
                    return IsRemoteUrl(steam.Image) ? "" : steam.Image;
            }
            return "";
        }

        private async void HandleBookshelfArtworkColor(Dictionary<string, object> message)
        {
            string gameId = GetString(message, "gameId");
            string path = BookshelfArtworkPath(gameId);
            if (path == "" || bookshelfArtworkPending.Count >= 8 || !bookshelfArtworkPending.Add(gameId))
            {
                if (!IsDisposed) Post(new Dictionary<string, object> { { "type", "bookshelfArtworkColor" }, { "gameId", gameId }, { "color", "" } });
                return;
            }
            string color = "";
            try
            {
                await bookshelfArtworkWorkers.WaitAsync();
                try { color = await Task.Run(() => BookshelfArtworkLighting.ReadColor(path)); }
                finally { bookshelfArtworkWorkers.Release(); }
            }
            catch { }
            finally { bookshelfArtworkPending.Remove(gameId); }
            // Recheck authoritative metadata if a game changed or became private during the read.
            if (IsDisposed || Disposing) return;
            if (!String.Equals(BookshelfArtworkPath(gameId), path, StringComparison.OrdinalIgnoreCase)) color = "";
            Post(new Dictionary<string, object> { { "type", "bookshelfArtworkColor" }, { "gameId", gameId }, { "color", color } });
        }
    }
}
