# Roda um batch da Visual com a trava ligada SÓ durante a compilação: espera a Unity livre, liga a trava, roda,
# e apaga a trava quando o log mostra o Setup pronto ("URP configurado" = scripts compilados) ou quando acaba.
param([string]$Metodo, [string]$Log, [string]$Extra = "", [int]$Min = 25, [string]$Oque = "captura")
$L = 'D:\unity\CSManager\Logs\unity_ocupado.txt'
while ((Test-Path $L) -or (Get-Process Unity -ErrorAction SilentlyContinue)) { Start-Sleep 5 }
"Visual: $Oque (compilando) $(Get-Date -Format 'HH:mm')" | Out-File $L -Encoding utf8
Remove-Item $Log -ErrorAction SilentlyContinue
"inicio $(Get-Date -Format 'HH:mm:ss')"
$job = Start-Job -ScriptBlock { param($m, $l, $e, $n) & D:\unity\CSManager\Tools\unity_run.ps1 -Metodo $m -Log $l -Extra $e -Min $n } -ArgumentList $Metodo, $Log, $Extra, $Min
try {
    while ($job.State -eq 'Running') {
        if ((Test-Path $L) -and (Test-Path $Log) -and (Select-String -Path $Log -Pattern 'URP configurado' -Quiet)) {
            Remove-Item $L -ErrorAction SilentlyContinue; "trava solta (compilou) $(Get-Date -Format 'HH:mm:ss')"
        }
        Start-Sleep 5
    }
    Receive-Job $job
} finally { Remove-Item $L -ErrorAction SilentlyContinue; Remove-Job $job -Force -ErrorAction SilentlyContinue }
if (Test-Path $Log) { Select-String -Path $Log -Pattern '\[Visual3D\]|Exception|error CS' | Select-Object -First 10 | ForEach-Object { $_.Line.Substring(0, [Math]::Min(250, $_.Line.Length)) } }
"fim $(Get-Date -Format 'HH:mm:ss')"
