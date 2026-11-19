<#
  Mesure réelle de NEXUS sur CETTE machine : temps jusqu'à la fenêtre, puis RAM et CPU échantillonnés au repos.
  Usage : powershell -ExecutionPolicy Bypass -File tools\measure.ps1 [-Exe chemin] [-Seconds 60] [-Out mesures.csv]
  Aucune valeur n'est inventée : le fichier produit contient uniquement ce qui a été relevé.
#>
param(
    [string]$Exe = "src\Nexus.App\bin\Release\net8.0-windows\NexusConsole.exe",
    [int]$Seconds = 60,
    [string]$Out = "mesures.csv"
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { throw "Exécutable introuvable : $Exe (compile d'abord avec setup.ps1)." }
Get-Process NexusConsole -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $Exe -PassThru
$p.Refresh()
while ($p.MainWindowHandle -eq 0 -and -not $p.HasExited -and $sw.Elapsed.TotalSeconds -lt 60) { Start-Sleep -Milliseconds 20; $p.Refresh() }
if ($p.HasExited) { throw "NEXUS s'est fermé pendant le démarrage (voir %LOCALAPPDATA%\NexusConsole\logs)." }
$startupMs = [int]$sw.Elapsed.TotalMilliseconds
Write-Host "Fenêtre visible après $startupMs ms. Laisse NEXUS au repos (n'y touche pas) pendant $Seconds s..."

Start-Sleep -Seconds 15   # laisse finir la détection initiale des jeux avant de mesurer le « repos »
$rows = @()
$last = (Get-Process -Id $p.Id).TotalProcessorTime
$t0 = Get-Date
for ($i = 0; $i -lt [math]::Ceiling($Seconds / 2); $i++) {
    Start-Sleep -Seconds 2
    $q = Get-Process -Id $p.Id
    $now = Get-Date
    $cpu = ($q.TotalProcessorTime - $last).TotalSeconds / ($now - $t0).TotalSeconds / [Environment]::ProcessorCount * 100
    $last = $q.TotalProcessorTime; $t0 = $now
    $rows += [pscustomobject]@{
        horodatage = $now.ToString("s"); ws_mo = [math]::Round($q.WorkingSet64 / 1MB, 1)
        prive_mo = [math]::Round($q.PrivateMemorySize64 / 1MB, 1); cpu_pct = [math]::Round($cpu, 2)
        processus_enfants = (Get-CimInstance Win32_Process -Filter "ParentProcessId=$($p.Id)" | Measure-Object).Count
    }
}
$rows | Export-Csv -Path $Out -NoTypeInformation -Encoding UTF8
$avg = { param($n) [math]::Round(($rows | Measure-Object -Property $n -Average).Average, 1) }
Write-Host ("Repos : RAM (working set) {0} Mo, mémoire privée {1} Mo, CPU {2} %  | démarrage {3} ms | échantillons : {4}" -f (& $avg 'ws_mo'), (& $avg 'prive_mo'), (& $avg 'cpu_pct'), $startupMs, $rows.Count)
Write-Host "Détail dans $Out. Pour la navigation et le chargement de la bibliothèque : onglet Performance de NEXUS (benchmark.csv)."
Stop-Process -Id $p.Id -Force
