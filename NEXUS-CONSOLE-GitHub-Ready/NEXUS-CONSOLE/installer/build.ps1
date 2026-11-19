<#
  Construit l'application puis l'installateur.
  Prérequis : SDK .NET 8+, et Inno Setup 6.3+ (iscc.exe dans le PATH, ou chemin dans $env:ISCC).
  Usage : powershell -ExecutionPolicy Bypass -File installer\build.ps1 [-Version 0.12.0] [-FrameworkDependent]
  Par défaut l'application est « autonome » (le runtime .NET est inclus : plus lourd, mais rien d'autre à installer).
  Avec -FrameworkDependent : plus léger, mais le .NET 8 Desktop Runtime doit déjà être présent.
#>
param([string]$Version = "0.12.0", [switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot "..")

if (Test-Path publish) { Remove-Item publish -Recurse -Force }
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }
$r2r = if ($FrameworkDependent) { "false" } else { "true" }
dotnet publish src/Nexus.App/Nexus.App.csproj -c Release -r win-x64 --self-contained $selfContained `
    -p:Version=$Version -p:PublishReadyToRun=$r2r -o publish
if ($LASTEXITCODE -ne 0) { throw "La publication a échoué." }

$iscc = if ($env:ISCC) { $env:ISCC } else { "iscc.exe" }
& $iscc "/DAppVersion=$Version" installer\NexusConsole.iss
if ($LASTEXITCODE -ne 0) { throw "La création de l'installateur a échoué." }
Write-Host "Installateur : dist\NexusConsole-Setup-$Version.exe"
Write-Host "Ensuite : suis documentation\CHECKLIST_INSTALLATION.md sur une machine ou une VM propre."
