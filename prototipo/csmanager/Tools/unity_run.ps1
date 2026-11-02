# Roda um método do projeto no Unity em batch, com vigia: se o Unity travar (passar de -Min minutos), mata o processo.
#   unity_run.ps1 -Metodo CSM.EditorTools.SimCli.Metrics -Log Logs\x.log [-Extra "-partidas 3"] [-Min 30]
param([string]$Metodo, [string]$Log, [string]$Extra = "", [int]$Min = 30)
$U = "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
$P = "D:\unity\CSManager"
if (-not [System.IO.Path]::IsPathRooted($Log)) { $Log = Join-Path $P $Log }
$p = Start-Process $U -ArgumentList "-batchmode -projectPath $P -executeMethod $Metodo $Extra -logFile `"$Log`"" -PassThru
if (-not $p.WaitForExit($Min * 60000)) { "VIGIA: Unity passou de $Min min, matando"; Stop-Process -Id $p.Id -Force; Start-Sleep 3 }
"saida $($p.ExitCode)"
Select-String -Path $Log -Pattern "error CS|Exception" | Select-Object -First 15 | ForEach-Object { $_.Line }
