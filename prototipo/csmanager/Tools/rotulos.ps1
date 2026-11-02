# Escreve os nomes dos lugares em cima da visão geral (Logs\render\mapa.png -> mapa_rotulos.png).
# Mesma projeção do Iso.Project: elevação 50°, S = 2 px/unidade, origem (1242, 106). Posições em quadradinhos da planta.
param([string]$Entrada = "D:\unity\CSManager\Logs\render\mapa.png", [string]$Saida = "D:\unity\CSManager\Logs\render\mapa_rotulos.png")
Add-Type -AssemblyName System.Drawing
$Q = 25; $GZ = 8; $UP = 38; $PLAT = 20
$el = 50 * [Math]::PI / 180; $r2 = [Math]::Sqrt(2)
function Proj($cx, $cy, $z) {
    $x = $cx * $Q; $y = $cy * $Q
    New-Object Drawing.PointF ([float](1242 + ($x - $y) / $r2 * 2)), ([float](106 + ($x + $y) * [Math]::Sin($el) / $r2 * 2 - $z * [Math]::Cos($el) * 2))
}
$src = [Drawing.Bitmap]::FromFile($Entrada); $bmp = New-Object Drawing.Bitmap $src; $src.Dispose()
$g = [Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAlias'
$f = New-Object Drawing.Font "Segoe UI", 22, ([Drawing.FontStyle]::Bold)
$bg = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(205, 20, 16, 16))
$cT = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 245, 170, 90)); $cC = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 130, 185, 255))
function L($t, $cx, $cy, $z, $b = [Drawing.Brushes]::White) {
    $p = Proj $cx $cy $z; $sz = $g.MeasureString($t, $f)
    $g.FillRectangle($bg, $p.X - $sz.Width / 2 - 4, $p.Y - $sz.Height / 2, $sz.Width + 8, $sz.Height)
    $g.DrawString($t, $f, $b, $p.X - $sz.Width / 2, $p.Y - $sz.Height / 2)
}
L "BASE CT" 20.5 3 $GZ $cC; L "BASE TR" 19.5 31.5 $GZ $cT
L "BOMB A" 35 9 $GZ; L "PLATAFORMA" 36.5 2.8 $PLAT; L "RUA DO CT" 28 2 $GZ $cC; L "DESCIDA CT" 30.5 4.5 $GZ $cC
L "BOMB B" 6 11 $GZ; L "BANCA" 3.5 7.5 $PLAT; L "CARROÇA" 7.5 14.3 ($GZ + 20); L "TAPETE" 3.5 16.5 $UP
L "CASARÃO (2º)" 7.5 28 $UP $cT
L "MEIO" 19 16 $GZ; L "TOPO DO MEIO" 22.5 26.5 $GZ $cT; L "JANELÃO (2º)" 18.5 7 $UP $cC; L "PRAÇA" 22.5 6.5 $GZ $cC
L "RATO" 15.5 10.5 $GZ; L "CONECTOR" 23.5 13 $GZ; L "CURTA" 13 17 $GZ; L "MERCADO" 15.5 4 $GZ $cC; L "VIELA" 7 2 $GZ $cC
L "PALÁCIO (2º)" 26 22 $UP $cT; L "SACADA" 30.5 12.5 $UP; L "RAMPA" 32.5 24 $GZ $cT
$bmp.Save($Saida); $g.Dispose(); $bmp.Dispose(); "ok"
