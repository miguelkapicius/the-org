# Junta os sprites renderizados numa folha (fundo de areia, ampliado sem borrar) pra conferir.
#   folha.ps1 -Time ct|tr [-Escala 2]
param([string]$Time = "ct", [int]$Escala = 2, [string]$Pasta = "D:\unity\CSManager\Logs\sprites")
Add-Type -AssemblyName System.Drawing
$linhas = @(
    @{ t = "MIRANDO - 8 DIREÇÕES"; f = (0..7 | ForEach-Object { Get-ChildItem "$Pasta\$($Time)_mira_$($_)_*.png" | Select-Object -First 1 }) },
    @{ t = "ARMAS: AK-47, M4A4, M4A1-S, AWP, MP9, MAC-10, USP-S, GLOCK-18"; f = (@("AK47", "M4A4", "M4A1S", "AWP", "MP9", "MAC10", "USP", "Glock") | ForEach-Object { "$Pasta\$($Time)_arma_$_.png" }) },
    @{ t = "RECARGA (1/2)"; f = (0..7 | ForEach-Object { "$Pasta\$($Time)_recarga_{0:D2}.png" -f $_ }) },
    @{ t = "RECARGA (2/2)"; f = (8..15 | ForEach-Object { "$Pasta\$($Time)_recarga_{0:D2}.png" -f $_ }) },
    @{ t = "ANDANDO"; f = (0..7 | ForEach-Object { "$Pasta\$($Time)_andar_{0:D2}.png" -f $_ }) }
)
$cel = 192 * $Escala; $cab = 34
$bmp = New-Object Drawing.Bitmap (8 * $cel), ($linhas.Count * ($cel + $cab))
$g = [Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'
$g.Clear([Drawing.Color]::FromArgb(201, 171, 124))
$f = New-Object Drawing.Font "Segoe UI", 15, ([Drawing.FontStyle]::Bold)
for ($r = 0; $r -lt $linhas.Count; $r++) {
    $y0 = $r * ($cel + $cab)
    $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 30, 26))), 0, $y0, 8 * $cel, $cab)
    $g.DrawString($linhas[$r].t, $f, [Drawing.Brushes]::White, 8, $y0 + 4)
    for ($c = 0; $c -lt 8; $c++) {
        $p = "$($linhas[$r].f[$c])"
        if (-not (Test-Path $p)) { continue }
        $im = [Drawing.Image]::FromFile($p)
        $g.DrawImage($im, $c * $cel, $y0 + $cab, $cel, $cel); $im.Dispose()
    }
}
$saida = "$Pasta\folha_$Time.png"; $bmp.Save($saida); $g.Dispose(); $bmp.Dispose(); $saida
