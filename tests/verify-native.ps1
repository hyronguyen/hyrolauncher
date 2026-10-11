
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference = 'Stop'
$root = $ProjectRoot
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Web.Extensions
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'dist\HYROLauncher.exe'))
$settingsType = $assembly.GetType('HYROLauncherWeb.LauncherSettings', $true)
if (!$settingsType.GetProperty('LibraryLayout')) { throw 'Built executable does not support LibraryLayout.' }
$settings = [Activator]::CreateInstance($settingsType)
$settings.LibraryLayout = 'carousel'
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$json = $serializer.Serialize($settings)
$roundtrip = $serializer.Deserialize($json, $settingsType)
if ($roundtrip.LibraryLayout -ne 'carousel') { throw 'Carousel setting did not survive serialization.' }
$rendererType = $assembly.GetType('HYROLauncherWeb.SvgIconRenderer', $true)
$renderArgs = New-Object object[] 3
$renderArgs[0] = [string][IO.Path]::Combine($root, 'assets\icons\reload.svg')
$renderArgs[1] = [int]96
$renderArgs[2] = [Drawing.Color]::Turquoise
$bitmap = $rendererType.GetMethod('Render').Invoke($null, $renderArgs)
try { $bitmap.Save((Join-Path $root 'reload-icon-check.png'), [Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }
Write-Output 'PASS: built EXE supports LibraryLayout, Carousel survives JSON roundtrip, reload icon renders.'
