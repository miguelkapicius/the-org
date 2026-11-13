param([string]$binA, [string]$nomeA, [string]$binB, [string]$nomeB, [string]$exp = "", [string]$seeds = "8100,8300,8500,8700", [int]$n = 450, [string]$expB = "__igual__")
# A/B pareado por semente entre dois binários do Headless (2 processos por vez). Saída: Logs/bots/<nome>_<seed>.txt
Set-Location D:\unity\CSManager
$dn = "$env:LOCALAPPDATA\dotnet9\dotnet.exe"
if ($expB -eq "__igual__") { $expB = $exp }
foreach ($s in $seeds.Split(',')) {
  $ps = @()
  foreach ($par in @(@($binA, $nomeA, $exp), @($binB, $nomeB, $expB))) {
    $saida = "$($par[1])_$s"
    if (Test-Path "Logs\bots\$saida.txt") { continue }
    $a = @("$($par[0])\Headless.dll", "-n", "$n", "-seed", "$s", "-auditar", "-saida", $saida)
    if ($par[2]) { $a += @("-exp", $par[2]) }
    $ps += Start-Process -FilePath $dn -ArgumentList $a -NoNewWindow -PassThru -RedirectStandardOutput "Logs\hd_$saida.out"
  }
  foreach ($p in $ps) { $p.WaitForExit() }
}
"FIM $nomeA x $nomeB"
