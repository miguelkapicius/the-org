# Build do exe 13 numa pasta à parte, com a trava ligada o tempo todo (pedido da Visual).
param([string]$pasta = "Build_teste13", [string]$log = "Logs\exe13_build.log")
$L = 'D:\unity\CSManager\Logs\unity_ocupado.txt'
Set-Location D:\unity\CSManager
while ((Test-Path $L) -or (Get-Process Unity -ErrorAction SilentlyContinue)) { Start-Sleep 5 }
"Bots (sessão a fundo): build do exe 13 $(Get-Date -Format 'HH:mm')" | Out-File $L -Encoding utf8
try {
  Remove-Item $log -ErrorAction SilentlyContinue
  "inicio $(Get-Date -Format 'HH:mm:ss')"
  & D:\unity\CSManager\Tools\unity_run.ps1 -Metodo CSM.EditorTools.Visual3D.GerarJogoBatch -Log $log -Extra "-v3d_build $pasta" -Min 25
} finally { Remove-Item $L -ErrorAction SilentlyContinue }
"fim $(Get-Date -Format 'HH:mm:ss')"
Select-String -Path $log -Pattern '\[Visual3D\] JOGO|Build Finished|changed while running|error CS' | Select-Object -First 10 | ForEach-Object { $_.Line.Substring(0, [Math]::Min(220, $_.Line.Length)) }
(Get-Item "$pasta\CSManager.exe").LastWriteTime
