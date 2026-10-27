# Builds AI-Pulse-Setup-<version>.exe on Windows.
#   .\scripts\build.ps1            # fetch newest app bundle, then build
#   .\scripts\build.ps1 -NoFetch   # reuse what's already in .\payload
#   .\scripts\build.ps1 -Preview   # look-only installer with a dummy app, for design work
# Needs the .NET SDK (8 or newer), and Node.js for the fetch step.
param([switch]$NoFetch, [switch]$Preview)
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

if ($Preview) {
  New-Item -ItemType Directory -Force build\dummy | Out-Null
  Set-Content "build\dummy\AI Pulse.exe" "preview"
  $payload = "$PWD\build\dummy"; $version = "0.0.0-preview"
} else {
  if (-not $NoFetch) { node scripts/fetch-bundle.js | Out-Host }
  $payload = "$PWD\payload\app"; $version = (Get-Content payload\VERSION -Raw).Trim()
}
dotnet build setup\Uninstall\Uninstall.csproj -c Release -p:Version=$version -o build\uninstall
dotnet build setup\Setup\Setup.csproj -c Release -p:Version=$version "-p:PayloadDir=$payload" "-p:UninstallerExe=$PWD\build\uninstall\Uninstall AI Pulse.exe" -o build\setup
New-Item -ItemType Directory -Force dist | Out-Null
Copy-Item build\setup\AI-Pulse-Setup.exe "dist\AI-Pulse-Setup-$version.exe"
Get-ChildItem dist\*.exe | ForEach-Object { "built $($_.Name) ($([math]::Round($_.Length/1MB,1)) MB)" }
if ($Preview) { "try it: dist\AI-Pulse-Setup-$version.exe --preview   (add --page remove|progress|done|error)" }
