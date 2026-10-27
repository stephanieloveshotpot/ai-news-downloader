# Builds AI-Pulse-Setup-<version>.exe on Windows.
#   .\scripts\build.ps1            # fetch newest app bundle, then compile
#   .\scripts\build.ps1 -NoFetch   # reuse what's already in .\payload
# Needs NSIS 3 (https://nsis.sourceforge.io) on PATH, and Node.js for the fetch step.
param([switch]$NoFetch)
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

if (-not $NoFetch) { node scripts/fetch-bundle.js | Tee-Object -Variable out | Out-Host }
$version = (Get-Content payload\VERSION -Raw).Trim()
New-Item -ItemType Directory -Force dist | Out-Null
makensis /V2 "/DVERSION=$version" "/DPAYLOAD=..\payload" "/DOUTDIR=..\dist" installer\AIPulse.nsi
Get-ChildItem dist\*.exe | ForEach-Object { "built $($_.Name) ($([math]::Round($_.Length/1MB,1)) MB)" }
