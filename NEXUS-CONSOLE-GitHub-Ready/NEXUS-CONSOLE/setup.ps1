# Crée la solution, ajoute les projets et compile. Prérequis : SDK .NET 8 ou plus récent (dotnet --version).
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not ((Test-Path NexusConsole.sln) -or (Test-Path NexusConsole.slnx))) { dotnet new sln -n NexusConsole }
dotnet sln add src/Nexus.Core/Nexus.Core.csproj src/Nexus.Data/Nexus.Data.csproj src/Nexus.Input/Nexus.Input.csproj src/Nexus.Detection/Nexus.Detection.csproj src/Nexus.Launching/Nexus.Launching.csproj src/Nexus.Cloud/Nexus.Cloud.csproj src/Nexus.Store/Nexus.Store.csproj src/Nexus.App/Nexus.App.csproj tests/Nexus.Data.Tests/Nexus.Data.Tests.csproj tests/Nexus.Logic.Tests/Nexus.Logic.Tests.csproj
dotnet build -c Release
dotnet test -c Release --no-build
Write-Host "Lancer : dotnet run --project src/Nexus.App -c Release"
