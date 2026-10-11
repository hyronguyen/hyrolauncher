
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
# Compile the production fullscreen methods against a minimal host; no launcher data or visible window is created.
$controllerSource=[IO.File]::ReadAllText((Join-Path $ProjectRoot 'ControllerInput.cs'))
$harness=@'
namespace HYROLauncherWeb {
 internal class TestView {internal object CoreWebView2=null;}
 internal partial class WebLauncherForm : System.Windows.Forms.Form {
  private readonly TestView webView=new TestView();
  private void Post(System.Collections.Generic.Dictionary<string,object> message) {}
  private void ApplyRoundedWindowRegion() {}
  public static string VerifyFullscreen() {
   using(var form=new WebLauncherForm()) {
    form.FormBorderStyle=System.Windows.Forms.FormBorderStyle.None;
    form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;
    form.Bounds=new System.Drawing.Rectangle(40,50,1280,720);
    form.MinimumSize=form.Size;form.MaximumSize=form.Size;
    var previous=form.Bounds;var minimum=form.MinimumSize;var maximum=form.MaximumSize;
    var monitor=System.Windows.Forms.Screen.FromControl(form).Bounds;
    form.SetBigPicture(true);
    if(form.Bounds!=monitor)throw new System.Exception("Big Picture does not fill the monitor: "+form.Bounds+" vs "+monitor);
    if(form.MaximumSize!=System.Drawing.Size.Empty)throw new System.Exception("Fullscreen size remains capped");
    form.SetBigPicture(false);
    if(form.Bounds!=previous || form.MinimumSize!=minimum || form.MaximumSize!=maximum)throw new System.Exception("Window bounds or size limits were not restored: "+form.Bounds+" expected "+previous+" min "+form.MinimumSize+" expected "+minimum+" max "+form.MaximumSize+" expected "+maximum);
    form.SetBigPicture(true);form.SetBigPicture(false);
    if(form.Bounds!=previous)throw new System.Exception("Repeated toggle changes window bounds");
    form.controllerTimer.Dispose();
    return "PASS: production Big Picture fills "+monitor.Width+"x"+monitor.Height+", restores 1280x720 bounds and size limits, and survives repeated toggles.";
   }
  }
 }
 public static class BigPictureWindowCheck {public static string Run(){return WebLauncherForm.VerifyFullscreen();}}
}
'@
Add-Type -TypeDefinition ($controllerSource+[Environment]::NewLine+$harness) -ReferencedAssemblies System.Windows.Forms,System.Drawing
[HYROLauncherWeb.BigPictureWindowCheck]::Run()
