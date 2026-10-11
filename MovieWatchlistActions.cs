using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HYROLauncherWeb
{
    internal partial class WebLauncherForm
    {
        private List<MovieWatchItem> pendingMovieImport;
        private string pendingMovieImportId;
        private async void OpenWatchlistMovie(string itemId)
        {
            PluginEntry p=state.Plugins.Find(x=>x.Id==BuiltInMoviePluginId);
            MovieWatchItem item=p==null||p.WatchItems==null?null:p.WatchItems.Find(x=>x.Id==itemId);
            if(item==null) return;
            HashSet<string> privateIds=GetPrivateGameIds(state.Games);
            if(IsPrivateMovieWatchItem(item,privateIds)) { Notify("Unlock the private movie before opening it."); return; }
            Uri uri;
            if(!Uri.TryCreate(item.Url,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https")) { Notify(state.Settings.Language=="vi"?"Hãy thêm link xem phim hợp lệ.":"Add a valid movie watch link."); return; }
            EnsureHyrowserWindow();
            await hyrowserWindow.EnsureWebViewAsync(webEnvironment);
            hyrowserWindow.OpenMovie(item);
            TcgStartMovieMonitor();
        }
        private void AdvanceBrowserMovie(string itemId,string url)
        {
            PluginEntry p=state.Plugins.Find(x=>x.Id==BuiltInMoviePluginId);
            MovieWatchItem item=p==null||p.WatchItems==null?null:p.WatchItems.Find(x=>x.Id==itemId);
            if(item==null) return;
            HashSet<string> privateIds=GetPrivateGameIds(state.Games);
            if(IsPrivateMovieWatchItem(item,privateIds)) return;
            int next=NextMovieEpisode(item.Episode);
            item.Episode=next.ToString();
            item.Status=item.TotalEpisodes>0&&next>=item.TotalEpisodes?"completed":"watching";
            if(!String.IsNullOrWhiteSpace(url)) item.Url=url;
            item.WatchTime=""; item.UpdatedAt=DateTime.UtcNow.ToString("o");
            SaveState(); SendState(); SendPluginState();
            if(hyrowserWindow!=null&&!hyrowserWindow.IsDisposed) hyrowserWindow.UpdateMovieContext(item);
        }
        internal static int NextMovieEpisode(string episode)
        {
            int current;
            // Accept legacy labels such as "Episode 3"; blank/non-numeric starts at episode 1.
            var match=System.Text.RegularExpressions.Regex.Match(episode??"",@"\d+");
            return match.Success&&Int32.TryParse(match.Value,out current)?Math.Min(9999,current)+1:1;
        }
        private void ExportWatchlistExcel()
        {
            try
            {
                var p=CreatePluginPanelState().Plugins.Find(x=>x.Id==BuiltInMoviePluginId);
                using(var dialog=new SaveFileDialog { Filter="Excel Workbook (*.xlsx)|*.xlsx",FileName="HYRO-Movie-Watchlist.xlsx",DefaultExt="xlsx",AddExtension=true })
                {
                    if(dialog.ShowDialog(pluginWindow!=null&&pluginWindow.Visible?(IWin32Window)pluginWindow:this)!=DialogResult.OK) return;
                    MovieWatchlistWorkbook.Export(dialog.FileName,p==null?new List<MovieWatchItem>():p.WatchItems);
                    MovieTransferResult(state.Settings.Language=="vi"?"Đã xuất danh sách phim.":"Movie list exported.");
                }
            }
            catch(Exception) { MovieTransferResult(state.Settings.Language=="vi"?"Không xuất được Excel. Hãy kiểm tra tệp có đang mở hay không.":"Unable to export. Check whether the workbook is open."); }
        }
        private void PreviewWatchlistImport()
        {
            try
            {
                using(var dialog=new OpenFileDialog { Filter="Excel Workbook (*.xlsx)|*.xlsx" })
                {
                    if(dialog.ShowDialog(pluginWindow!=null&&pluginWindow.Visible?(IWin32Window)pluginWindow:this)!=DialogResult.OK) return;
                    pendingMovieImport=MovieWatchlistWorkbook.Import(dialog.FileName); pendingMovieImportId=Guid.NewGuid().ToString("N");
                    if(pluginWindow!=null) pluginWindow.Post(new Dictionary<string,object> { {"type","movieImportPreview"},{"requestId",pendingMovieImportId},{"count",pendingMovieImport.Count},{"fileName",Path.GetFileName(dialog.FileName)} });
                }
            }
            catch(Exception ex) { pendingMovieImport=null; MovieTransferResult((state.Settings.Language=="vi"?"Không đọc được Excel: ":"Unable to import Excel: ")+ex.Message); }
        }
        private void CommitWatchlistImport(string requestId)
        {
            if(pendingMovieImport==null||requestId!=pendingMovieImportId) return;
            PluginEntry p=state.Plugins.Find(x=>x.Id==BuiltInMoviePluginId); if(p==null) return;
            if(p.WatchItems==null) p.WatchItems=new List<MovieWatchItem>();
            HashSet<string> privateIds=GetPrivateGameIds(state.Games);
            foreach(var imported in pendingMovieImport)
            {
                var existing=p.WatchItems.Find(x=>(!String.IsNullOrWhiteSpace(imported.Id)&&x.Id==imported.Id)||(!String.IsNullOrWhiteSpace(imported.Url)&&x.Url==imported.Url));
                if(existing!=null&&IsPrivateMovieWatchItem(existing,privateIds)) continue;
                imported.Id=existing!=null?existing.Id:String.IsNullOrWhiteSpace(imported.Id)?Guid.NewGuid().ToString("N"):imported.Id;
                imported.SourceGameId=existing==null?"":existing.SourceGameId;
                imported.Status=NormalizeMovieStatus(imported.Status,imported.Episode,imported.WatchTime);
                imported.UpdatedAt=String.IsNullOrWhiteSpace(imported.UpdatedAt)?DateTime.UtcNow.ToString("o"):imported.UpdatedAt;
                // Local poster paths remain references; importing does not delete or overwrite artwork.
                imported.PosterPath=ResolveStoredAssetPath(imported.PosterPath);
                if(existing==null) p.WatchItems.Add(imported); else p.WatchItems[p.WatchItems.IndexOf(existing)]=imported;
            }
            pendingMovieImport=null; pendingMovieImportId="";
            SaveState(); SendState(); SendPluginState();
            MovieTransferResult(state.Settings.Language=="vi"?"Đã nhập danh sách phim.":"Movie list imported.");
        }
        private void MovieTransferResult(string message)
        {
            if(pluginWindow!=null&&!pluginWindow.IsDisposed) pluginWindow.Post(new Dictionary<string,object> { {"type","movieTransferResult"},{"message",message} });
        }
    }
}
