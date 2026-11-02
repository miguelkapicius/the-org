# Monta folhas de conferência a partir do manifest_<time>.txt (rótulo|arquivo|arquivo...), 7 linhas por folha.
#   folha2.ps1 -Time ct|tr [-Pasta ...] [-Escala 1]
param([string]$Time = "ct", [string]$Pasta = "D:\unity\CSManager\Logs\sprites", [double]$Escala = 1.0, [int]$PorFolha = 7)
Add-Type -AssemblyName System.Drawing
$rows = New-Object 'System.Collections.Generic.List[string[]]'
foreach ($l in (Get-Content "$Pasta\manifest_$Time.txt" -Encoding UTF8)) { if ($l -match '\|') { $rows.Add($l.Split('|')) } }
$cel = [int](192 * $Escala); $cab = 26
$f = New-Object Drawing.Font "Segoe UI", 12, ([Drawing.FontStyle]::Bold)
$saidas = @()
for ($ini = 0; $ini -lt $rows.Count; $ini += $PorFolha) {
    $bloco = $rows.GetRange($ini, [math]::Min($PorFolha, $rows.Count - $ini))
    $cols = 0; foreach ($rw in $bloco) { $cols = [math]::Max($cols, $rw.Length - 1) }
    $bmp = New-Object Drawing.Bitmap ($cols * $cel), ($bloco.Count * ($cel + $cab))
    $g = [Drawing.Graphics]::FromImage($bmp); $g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'
    $g.Clear([Drawing.Color]::FromArgb(201, 171, 124))
    for ($r = 0; $r -lt $bloco.Count; $r++) {
        $y0 = $r * ($cel + $cab); $row = $bloco[$r]
        $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 30, 26))), 0, $y0, $cols * $cel, $cab)
        $g.DrawString($row[0].ToUpper(), $f, [Drawing.Brushes]::White, 6, $y0 + 3)
        for ($c = 1; $c -lt $row.Count; $c++) {
            if (-not (Test-Path $row[$c])) { continue }
            $im = [Drawing.Image]::FromFile($row[$c]); $g.DrawImage($im, ($c - 1) * $cel, $y0 + $cab, $cel, $cel); $im.Dispose()
        }
    }
    $n = [math]::Floor($ini / $PorFolha) + 1
    $s = "$Pasta\folha_$($Time)_$n.png"; $bmp.Save($s); $g.Dispose(); $bmp.Dispose(); $saidas += $s
}
$saidas
