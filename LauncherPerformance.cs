using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HYROLauncherWeb
{
    // WebView2 needs its native visibility refreshed when an ancestor is hidden.
    // WinForms' effective Visible value can change without setting this control's
    // own visibility bit, leaving the browser controller visible to TrySuspend.
    internal sealed class LauncherWebView : WebView2
    {
        internal void RefreshNativeVisibility() {base.OnVisibleChanged(EventArgs.Empty);}
    }
    // Native startup surface: visible before the browser runtime has been created.
    internal sealed class LauncherLoadingPanel : Panel
    {
        private readonly Label status = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Button close = new Button();
        private readonly Color accent;
        internal LauncherLoadingPanel(Color color, bool vietnamese)
        {
            accent=color; Dock=DockStyle.Fill; DoubleBuffered=true;
            BackColor=Color.FromArgb(9,13,22);
            status.Text=vietnamese?"Đang khởi động launcher…":"Starting launcher…";
            status.ForeColor=Color.FromArgb(151,164,185);status.BackColor=Color.Transparent;
            status.Font=new Font("Segoe UI",10F);status.TextAlign=ContentAlignment.MiddleCenter;
            progress.Style=ProgressBarStyle.Marquee;progress.MarqueeAnimationSpeed=26;
            Controls.Add(status);Controls.Add(progress);
            close.Text=vietnamese?"Đóng":"Close";close.Visible=false;close.FlatStyle=FlatStyle.Flat;
            close.FlatAppearance.BorderColor=color;close.ForeColor=color;close.BackColor=Color.FromArgb(19,27,39);
            close.Click+=delegate {var owner=FindForm() as WebLauncherForm;if(owner!=null)owner.ExitFailedStartup();};Controls.Add(close);
            Resize+=delegate {PlaceControls();};PlaceControls();
        }
        private void PlaceControls()
        {
            status.SetBounds(Math.Max(0,(Width-460)/2),Height/2+30,Math.Min(460,Width),48);
            progress.SetBounds(Math.Max(0,(Width-180)/2),Height/2+95,180,4);
            close.SetBounds(Math.Max(0,(Width-100)/2),Height/2+110,100,34);
        }
        internal void Failure(string text) {progress.Visible=false;status.Text=text;close.Visible=true;}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using(var brush=new LinearGradientBrush(ClientRectangle,Color.FromArgb(19,31,46),BackColor,65F))e.Graphics.FillRectangle(brush,ClientRectangle);
            using(var font=new Font("Segoe UI",36F,FontStyle.Bold))
            using(var brush=new SolidBrush(accent))
            using(var format=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                e.Graphics.DrawString("HYRO",font,brush,new RectangleF(0,Height/2-80,Width,80),format);
        }
    }

    internal partial class WebLauncherForm
    {
        private LauncherLoadingPanel startupSurface;
        private Task linkInitialization,feedInitialization;
        private bool linkRequested,feedRequested;
        private int linkRequestVersion,feedRequestVersion;
        private void InitializeStartupSurface()
        {
            Color color;try {color=ColorTranslator.FromHtml(state.Settings.AccentColor);}catch {color=Color.FromArgb(64,199,224);}
            startupSurface=new LauncherLoadingPanel(color,state.Settings.Language=="vi");Controls.Add(startupSurface);startupSurface.BringToFront();
        }
        private void FinishStartupSurface()
        {
            if(startupSurface==null)return;Controls.Remove(startupSurface);startupSurface.Dispose();startupSurface=null;
        }
        internal void ExitFailedStartup() {exitRequested=true;Close();}
        internal static async Task SetViewActivityAsync(WebView2 view,bool active,bool keepAudio,bool keepRunning=false)
        {
            if(view==null || view.IsDisposed || view.CoreWebView2==null)return;
            try
            {
                var core=view.CoreWebView2;
                var launcherView=view as LauncherWebView;if(launcherView!=null)launcherView.RefreshNativeVisibility();
                if(active)
                {
                    if(core.IsSuspended)core.Resume();
                    else core.MemoryUsageTargetLevel=CoreWebView2MemoryUsageTargetLevel.Normal;
                }
                else if(keepRunning || (keepAudio && core.IsDocumentPlayingAudio))
                    core.MemoryUsageTargetLevel=CoreWebView2MemoryUsageTargetLevel.Low;
                else
                {
                    bool suspended=false;
                    try {suspended=await core.TrySuspendAsync();}catch { }
                    // A visibility change can race the asynchronous suspension.
                    if(!view.IsDisposed && view.Visible && view.FindForm()!=null && view.FindForm().Visible && view.FindForm().WindowState!=FormWindowState.Minimized)
                    {
                        if(core.IsSuspended)core.Resume();
                        else core.MemoryUsageTargetLevel=CoreWebView2MemoryUsageTargetLevel.Normal;
                    }
                    else if(!view.IsDisposed && !suspended)core.MemoryUsageTargetLevel=CoreWebView2MemoryUsageTargetLevel.Low;
                }
            }
            catch { /* Older installed runtimes can decline suspension. */ }
        }
        private void SyncLauncherActivity()
        {
            bool active=Visible && WindowState!=FormWindowState.Minimized;
            if(webView.CoreWebView2!=null)
            {
                if(active)webView.CoreWebView2.Resume();
                Post(new System.Collections.Generic.Dictionary<string,object>{{"type","launcherActivity"},{"active",active}});
            }
            ObserveViewActivity(webView,active,true);
            ObserveViewActivity(linkView,active&&linkRequested,true);
            ObserveViewActivity(feedView,active&&feedRequested,false);
        }
        private static async void ObserveViewActivity(WebView2 view,bool active,bool keepAudio) {await SetViewActivityAsync(view,active,keepAudio);}
        private Task EnsureAuxiliaryView(bool feed)
        {
            if(feed)
            {if(feedInitialization==null || feedInitialization.IsFaulted || feedInitialization.IsCanceled)feedInitialization=InitializeAuxiliaryView(feedView,true);return feedInitialization;}
            if(linkInitialization==null || linkInitialization.IsFaulted || linkInitialization.IsCanceled)linkInitialization=InitializeAuxiliaryView(linkView,false);return linkInitialization;
        }
        private async Task InitializeAuxiliaryView(WebView2 view,bool feed)
        {
            try
            {
                await view.EnsureCoreWebView2Async(webEnvironment);
                if(IsDisposed || view.IsDisposed)return;
                view.CoreWebView2.Settings.AreDefaultContextMenusEnabled=true;
                if(feed){view.CoreWebView2.IsMuted=true;view.ZoomFactor=.25;}
                else view.CoreWebView2.SourceChanged+=delegate
                {
                    try {currentWebLinkUrl=view.Source==null?currentWebLinkUrl:view.Source.ToString();Post(new System.Collections.Generic.Dictionary<string,object>{{"type","webLinkLocation"},{"url",currentWebLinkUrl}});}catch { }
                };
                view.CoreWebView2.NavigationCompleted+=delegate {SyncLauncherActivity();};
            }
            catch {if(feed)feedInitialization=null;else linkInitialization=null;throw;}
        }
    }
}
