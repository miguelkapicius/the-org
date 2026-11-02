# Vídeos em tempo real das 8 armas: recarga (cada uma no seu tempo) e cadência (2 s atirando direto).
#   video_tempos.ps1 [-Time tr]
param([string]$Time = "tr", [string]$Pasta = "D:\unity\CSManager\Logs\recarga_video")
Add-Type -AssemblyName System.Drawing
$B = "D:\SteamLibrary\steamapps\common\Blender\blender.exe"
$ws = "AK47","M4A4","M4A1S","AWP","MP9","MAC10","USP","Glock"; $nm = "AK-47","M4A4","M4A1-S","AWP","MP9","MAC-10","USP-S","GLOCK-18"
$cs2 = @{ AK47 = @(2.4, 600); M4A4 = @(3.1, 666.67); M4A1S = @(3.1, 600); AWP = @(3.7, 41.24); MP9 = @(2.1, 857.14); MAC10 = @(2.6, 800); USP = @(2.2, 352.94); Glock = @(2.3, 400) }
$cel = 384; $cab = 30; $f = New-Object Drawing.Font "Segoe UI", 13, ([Drawing.FontStyle]::Bold)
$barra = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40,30,26))
function Grade($dir, $fps, $nq, $sb) {
    if (Test-Path $dir) { Get-ChildItem $dir -Filter *.png | ForEach-Object { [IO.File]::Delete($_.FullName) } } else { New-Item -ItemType Directory $dir | Out-Null }
    for ($n = 0; $n -lt $nq; $n++) {
        $bmp = New-Object Drawing.Bitmap (4*$cel), (2*($cel+$cab)); $g = [Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'; $g.Clear([Drawing.Color]::FromArgb(201,171,124))
        for ($i = 0; $i -lt 8; $i++) {
            $x = ($i % 4) * $cel; $y = [math]::Floor($i / 4) * ($cel + $cab)
            $r = & $sb $ws[$i] ($n / $fps)
            $g.FillRectangle($barra, $x, $y, $cel, $cab); $g.DrawString($r.titulo, $f, [Drawing.Brushes]::White, $x + 6, $y + 4)
            $im = [Drawing.Image]::FromFile($r.arquivo)
            $g.DrawImage($im, (New-Object Drawing.Rectangle $x, ($y + $cab), $cel, $cel), (New-Object Drawing.Rectangle 0, 0, 192, 192), [Drawing.GraphicsUnit]::Pixel); $im.Dispose()
            if ($r.prog -ge 0) {   # barra de progresso da recarga
                $g.FillRectangle([Drawing.Brushes]::DimGray, $x + 20, $y + $cab + $cel - 22, $cel - 40, 10)
                $g.FillRectangle($(if ($r.prog -ge 1) { [Drawing.Brushes]::LimeGreen } else { [Drawing.Brushes]::Orange }), $x + 20, $y + $cab + $cel - 22, ($cel - 40) * [math]::Min(1, $r.prog), 10)
            }
        }
        $bmp.Save(("$dir\q_{0:D4}.png" -f $n)); $g.Dispose(); $bmp.Dispose()
    }
}
# recarga: 12 qps, cada arma no seu tempo, segura no fim; 4,2 s de vídeo, 2 vezes
$recDir = "$Pasta\v_recarga"
Grade $recDir 12 50 {
    param($w, $s)
    $files = @(Get-ChildItem "$Pasta\$($Time)_rv_$($w)_*.png" | Sort-Object Name)
    $T = $cs2[$w][0] * 0.9
    $k = [math]::Min($files.Count - 1, [math]::Floor($s * 12))
    @{ titulo = ("{0}  recarga {1:0.00} s" -f $nm[[array]::IndexOf($ws, $w)], $T); arquivo = $files[$k].FullName; prog = $s / $T }
}
$n = 50; for ($r = 0; $r -lt 50; $r++) { Copy-Item ("$recDir\q_{0:D4}.png" -f $r) ("$recDir\q_{0:D4}.png" -f $n); $n++ }
& $B -b --factory-startup --python D:\unity\EsportsPoc\Tools\blender\frames_to_mp4.py -- $recDir D:\unity\CSManager\Logs\render\recargas_tempo_real.mp4 12 2>&1 | Select-String "MP4 OK|Error"
# cadência: 24 qps, 2,5 s atirando direto. Quadro do disparo = coice com clarão; o seguinte = voltando; senão parado.
# AWP usa o ciclo inteiro (coice + ferrolho). Cápsulas: saem da janela de ejeção em arco, caem no chão perto do pé
# e ficam lá.
$pontos = Get-Content "$Pasta\$($Time)_pontos.json" -Raw | ConvertFrom-Json
$cadDir = "$Pasta\v_cadencia"
if (Test-Path $cadDir) { Get-ChildItem $cadDir -Filter *.png | ForEach-Object { [IO.File]::Delete($_.FullName) } } else { New-Item -ItemType Directory $cadDir | Out-Null }
$lat = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(214, 170, 70)); $latE = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(120, 84, 30))
$FPS = 24; $NQ = 60
for ($n = 0; $n -lt $NQ; $n++) {
    $s = $n / $FPS
    $bmp = New-Object Drawing.Bitmap (4*$cel), (2*($cel+$cab)); $g = [Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'; $g.Clear([Drawing.Color]::FromArgb(201,171,124))
    for ($i = 0; $i -lt 8; $i++) {
        $w = $ws[$i]; $x0 = ($i % 4) * $cel; $y0 = [math]::Floor($i / 4) * ($cel + $cab)
        $iv = 60.0 / $cs2[$w][1] * 0.9
        $k = [math]::Floor($s / $iv); $tiros = $k + 1
        $awp = $w -eq "AWP"
        if ($awp) { $arq = ("$Pasta\$($Time)_awp_{0:D2}.png" -f [math]::Min(15, [math]::Floor((($s % $iv) / $iv) * 16))) }
        else {
            $d = $s - $k * $iv
            $q = if ($d -lt 1.0 / $FPS) { 2 } elseif ($d -lt 2.0 / $FPS) { 1 } else { 0 }
            $arq = "$Pasta\$($Time)_coice_$($w)_$q.png"
        }
        $g.FillRectangle($barra, $x0, $y0, $cel, $cab)
        $g.DrawString(("{0}  {1:0} RPM  tiros: {2}" -f $nm[$i], (60.0 / $iv), $tiros), $f, [Drawing.Brushes]::White, $x0 + 6, $y0 + 4)
        $im = [Drawing.Image]::FromFile($arq)
        $g.DrawImage($im, (New-Object Drawing.Rectangle $x0, ($y0 + $cab), $cel, $cel), (New-Object Drawing.Rectangle 0, 0, 192, 192), [Drawing.GraphicsUnit]::Pixel); $im.Dispose()
        # cápsulas de todos os tiros até agora
        $p = $pontos.$w; $ex = $p.ejecao[0]; $ey = $p.ejecao[1]
        for ($j = 0; $j -le $k; $j++) {
            $tj = $j * $iv + $(if ($awp) { 0.55 * $iv } else { 0.0 })
            $age = $s - $tj; if ($age -lt 0) { continue }
            $rnd = [math]::Abs([math]::Sin(($j + 1) * 12.9898 + $i * 78.233) * 43758.5453) % 1
            $vx = -10 - 14 * $rnd; $vy = -48 - 18 * $rnd; $gr = 300
            $chao = $p.pe[1] + 2 + 10 * $rnd
            $tl = (-$vy + [math]::Sqrt($vy * $vy + 2 * $gr * ($chao - $ey))) / $gr
            if ($age -lt $tl) { $cx = $ex + $vx * $age; $cy = $ey + $vy * $age + 0.5 * $gr * $age * $age; $voando = $true }
            else { $cx = $ex + $vx * $tl + 2 * $rnd; $cy = $chao; $voando = $false }
            $X = $x0 + $cx * 2; $Y = $y0 + $cab + $cy * 2
            $hor = $voando -and ([math]::Floor($age * 30) % 2 -eq 0)
            $ww = $(if ($hor -or -not $voando) { 6 } else { 3 }); $hh = $(if ($hor -or -not $voando) { 3 } else { 6 })
            $g.FillRectangle($latE, $X - 1, $Y - 1, $ww + 2, $hh + 2); $g.FillRectangle($lat, $X, $Y, $ww, $hh)
        }
    }
    $bmp.Save(("$cadDir\q_{0:D4}.png" -f $n)); $g.Dispose(); $bmp.Dispose()
}
& $B -b --factory-startup --python D:\unity\EsportsPoc\Tools\blender\frames_to_mp4.py -- $cadDir D:\unity\CSManager\Logs\render\cadencia_tempo_real.mp4 24 2>&1 | Select-String "MP4 OK|Error"