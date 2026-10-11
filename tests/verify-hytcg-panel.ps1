
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
$taskSource=[IO.File]::ReadAllText((Join-Path $ProjectRoot 'WebLauncher.cs'))
$taskClassStart=$taskSource.IndexOf('internal sealed class PluginBrowserWindow')
$taskMethodStart=$taskSource.IndexOf('        public void DockToOwner()',$taskClassStart)
$taskMethodEnd=$taskSource.IndexOf('        public void ReloadPlugin()',$taskMethodStart)
$taskMethods=$taskSource.Substring($taskMethodStart,$taskMethodEnd-$taskMethodStart)
$taskHarness=@'
using System;using System.Drawing;using System.Windows.Forms;
public class HyTcgPanelHarness:Form {
 private Form ownerForm;private bool docked=true,widePanel;
 private string currentUrl="file:///hytcg.html?id=builtin.hytcg";
 private Size defaultDockSize=new Size(520,640),rememberedPanelSize=new Size(520,640),compactPanelSize=new Size(520,640);
 protected override void SetVisibleCore(bool value){if(!value)base.SetVisibleCore(false);}
 public static string Run(){
  using(var owner=new Form())using(var panel=new HyTcgPanelHarness()){
   owner.Bounds=new Rectangle(30,40,1280,720);panel.ownerForm=owner;panel.MinimumSize=new Size(420,320);panel.Size=new Size(520,640);
   panel.Resize+=panel.RememberPanelSize;
   panel.DockToOwner();if(!panel.docked)throw new Exception("Panel lost docking state");var compact=panel.Size;var work=Screen.FromControl(owner).WorkingArea;
   panel.ToggleWidePanel();if(panel.Width!=Math.Min(1120,work.Width-32)||panel.Height!=Math.Min(880,work.Height-32))throw new Exception("Expanded panel is capped at compact dimensions");
   if(!work.Contains(panel.Bounds))throw new Exception("Expanded panel escapes working area");
   panel.DockToOwner();if(!panel.widePanel)throw new Exception("Owner move drops expanded state");
   panel.ToggleWidePanel();if(panel.Size!=compact)throw new Exception("Compact panel size was not restored");
   panel.ToggleWidePanel();panel.ToggleWidePanel();if(panel.Size!=compact)throw new Exception("Repeated toggle changes compact size");
   panel.currentUrl="file:///plugin.html";panel.ToggleWidePanel();if(panel.Size!=compact)throw new Exception("Other plugins can trigger HYTCG expansion");
   return "PASS: production panel methods expand to working area, restore compact size, preserve docking and restrict expansion to HYTCG; no visible window or production writes.";
  }
 }
'@
Add-Type -TypeDefinition ($taskHarness+$taskMethods+'}') -ReferencedAssemblies System.Windows.Forms,System.Drawing
[HyTcgPanelHarness]::Run()
