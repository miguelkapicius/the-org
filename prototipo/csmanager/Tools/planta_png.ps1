# Desenha a planta (Tools\planta.txt) vista de cima: cor por tipo de chão, nomes, rotas TR/CT e a mira do janelão.
#   planta_png.ps1 [-Saida Logs\render\planta.png]
param([string]$Saida = "D:\unity\CSManager\Logs\render\planta.png")
$lines = Get-Content D:\unity\CSManager\Tools\planta.txt -Encoding UTF8 | Where-Object { $_.Length -ge 40 }
Add-Type -AssemblyName System.Drawing
$S = 24; $W = 40 * $S; $H = 35 * $S
$bmp = New-Object Drawing.Bitmap ($W + 260), $H
$g = [Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.Clear([Drawing.Color]::FromArgb(40, 32, 30))
$cor = New-Object 'System.Collections.Generic.Dictionary[char,System.Drawing.Color]'
$cor['#'] = [Drawing.Color]::FromArgb(150, 84, 62); $cor['T'] = [Drawing.Color]::FromArgb(236, 190, 120); $cor['C'] = [Drawing.Color]::FromArgb(150, 190, 230)
$cor['a'] = [Drawing.Color]::FromArgb(240, 214, 160); $cor['b'] = [Drawing.Color]::FromArgb(240, 214, 160); $cor['r'] = [Drawing.Color]::FromArgb(214, 186, 138)
$cor['m'] = [Drawing.Color]::FromArgb(228, 204, 156); $cor['p'] = [Drawing.Color]::FromArgb(180, 170, 160); $cor['k'] = [Drawing.Color]::FromArgb(160, 112, 72)
$cor['w'] = [Drawing.Color]::FromArgb(176, 166, 200); $cor['P'] = [Drawing.Color]::FromArgb(196, 188, 170); $cor['n'] = [Drawing.Color]::FromArgb(196, 188, 170)
$cor['s'] = [Drawing.Color]::FromArgb(120, 120, 130); $cor['~'] = [Drawing.Color]::FromArgb(230, 120, 90); $cor['v'] = [Drawing.Color]::FromArgb(90, 150, 90)
for ($y = 0; $y -lt 35; $y++) { for ($x = 0; $x -lt 40; $x++) { $g.FillRectangle((New-Object Drawing.SolidBrush $cor[$lines[$y][$x]]), $x * $S, $y * $S, $S, $S) } }
$pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(40, 0, 0, 0))
for ($x = 0; $x -le 40; $x++) { $g.DrawLine($pen, $x * $S, 0, $x * $S, $H) }; for ($y = 0; $y -le 35; $y++) { $g.DrawLine($pen, 0, $y * $S, $W, $y * $S) }
function Pt($x, $y) { New-Object Drawing.PointF ([float](($x + 0.5) * $S)), ([float](($y + 0.5) * $S)) }
function Rota($c, [double[]]$pts) {
    $pn = New-Object Drawing.Pen $c, 5; $pn.CustomEndCap = New-Object Drawing.Drawing2D.AdjustableArrowCap 4, 4; $pn.DashStyle = 'Dash'
    $arr = @(); for ($i = 0; $i -lt $pts.Count; $i += 2) { $arr += (Pt $pts[$i] $pts[$i + 1]) }; $g.DrawLines($pn, [Drawing.PointF[]]$arr)
}
$tr = [Drawing.Color]::FromArgb(230, 235, 120, 30); $ct = [Drawing.Color]::FromArgb(230, 40, 110, 220)
Rota $tr @(26, 30, 32, 30, 32, 17, 36, 17, 36, 12)
Rota $tr @(25.5, 28.5, 25.5, 16.5, 28.5, 16.5, 28.5, 12.5)
Rota $tr @(22.5, 28, 22.5, 24, 19, 23.5, 19, 13, 25.5, 12.5, 25.5, 9.5, 30.5, 9.5)
Rota $tr @(18, 21, 12.5, 20.5, 12.5, 12.5, 10, 12.5)
Rota ([Drawing.Color]::FromArgb(230, 240, 220, 60)) @(12.5, 14, 14, 13, 14, 8, 14.5, 6.5, 16.5, 6.5)
Rota $tr @(14, 30.5, 10.5, 30.5, 6.5, 27, 4.5, 20, 4.5, 16.5)
Rota $ct @(27, 2, 31, 1.5, 33, 2.5)
Rota $ct @(27, 3, 30, 3, 30, 4.5, 32, 4.5)
Rota $ct @(19.5, 3.5, 13, 3.5, 10, 5.5)
Rota $ct @(12, 3, 11.5, 1.5, 4, 1.5, 3.5, 5.5)
Rota $ct @(7, 11, 6, 14.6, 5.5, 16.3)
Rota $ct @(22.5, 4, 22.5, 8, 19, 9, 19, 14)
$cone = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(70, 120, 60, 200))
$g.FillPolygon($cone, [Drawing.PointF[]]@((Pt 16.2 7.6), (Pt 20.8 7.6), (Pt 20.8 24.5), (Pt 16.2 24.5)))
$f = New-Object Drawing.Font "Segoe UI", 10, ([Drawing.FontStyle]::Bold); $bg = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(200, 20, 16, 16))
function L($t, $x, $y) { $p = Pt $x $y; $sz = $g.MeasureString($t, $f); $g.FillRectangle($bg, $p.X - $sz.Width / 2, $p.Y - $sz.Height / 2, $sz.Width, $sz.Height); $g.DrawString($t, $f, [Drawing.Brushes]::White, $p.X - $sz.Width / 2, $p.Y - $sz.Height / 2) }
L "BASE CT" 23 2.5; L "BASE TR" 19.5 31.5; L "BOMB A" 35 8; L "PLATAFORMA +8" 36 2.5; L "BOMB B" 6 11; L "BANCA +8" 3 7; L "RAMPA" 32.5 24
L "PALÁCIO +14" 25.5 21; L "SACADA (queda)" 30 12; L "MEIO" 18.5 17; L "PRAÇA" 23 7.5; L "JANELÃO +14" 18 6.5; L "CONECTOR" 23 12.5
L "CURTA" 12.5 17; L "RATO" 14 10.5; L "MERCADO" 15 3.5; L "VIELA" 7 1.5; L "CASARÃO +10" 7 27; L "TAPETE (queda)" 3.5 16.4; L "CARROÇA" 6 14.2; L "TOPO DO MEIO" 22.5 26; L "RUA DO B" 14 30.5; L "RUA DO CT" 29.5 1.5
$lx = $W + 12; $ly = 12
foreach ($it in @(@('#', 'prédio'), @('r', 'rua (chão)'), @('m', 'meio'), @('a', 'bomb'), @('P', 'plataforma/banca +8'), @('p', 'palácio/janelão +14'), @('k', 'casarão +10'), @('s', 'escada'), @('~', 'sacada (queda)'), @('v', 'carroça: sobe pulando'))) {
    $g.FillRectangle((New-Object Drawing.SolidBrush $cor[[char]$it[0]]), $lx, $ly, 18, 18); $g.DrawString($it[1], $f, [Drawing.Brushes]::White, $lx + 24, $ly); $ly += 26
}
$ly += 10; $g.DrawString("--> rota TR", $f, (New-Object Drawing.SolidBrush $tr), $lx, $ly); $ly += 22
$g.DrawString("--> caminho de rato", $f, [Drawing.Brushes]::Gold, $lx, $ly); $ly += 22
$g.DrawString("--> rota CT", $f, (New-Object Drawing.SolidBrush $ct), $lx, $ly); $ly += 22
$g.DrawString("roxo: mira do janelão", $f, [Drawing.Brushes]::Plum, $lx, $ly); $ly += 30
$g.DrawString("1 quadradinho = 20 unid.`n10 quadr. = 4 s correndo", $f, [Drawing.Brushes]::Gainsboro, $lx, $ly)
$bmp.Save($Saida); "ok"
