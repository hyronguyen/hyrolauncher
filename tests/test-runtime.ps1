# Shared paths for the isolated verification scripts.
$TestSourceRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$ProjectRoot = [IO.Path]::GetFullPath((Join-Path $TestSourceRoot '..'))
$VerificationRoot = Join-Path $ProjectRoot 'output/verification'
if (!(Test-Path -LiteralPath $VerificationRoot)) {
    New-Item -ItemType Directory -Path $VerificationRoot -Force | Out-Null
}
function Resolve-TestSource([string]$Name) {
    if ($Name -like 'verify-*') { return Join-Path $TestSourceRoot $Name }
    return Join-Path $ProjectRoot $Name
}
function Get-TestExecutable {
    if ($env:HYRO_TEST_EXE) { return [IO.Path]::GetFullPath($env:HYRO_TEST_EXE) }
    $checkBuild = Join-Path $ProjectRoot 'dist/HYROLauncher.Check.exe'
    if (Test-Path -LiteralPath $checkBuild) { return $checkBuild }
    $releaseBuild = Join-Path $ProjectRoot 'dist/HYROLauncher.exe'
    if (Test-Path -LiteralPath $releaseBuild) { return $releaseBuild }
    throw 'Build the launcher first, or set HYRO_TEST_EXE to a candidate executable.'
}
