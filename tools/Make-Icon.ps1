$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 64,64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = 'AntiAlias'
$graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#166E62'))
$cream = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#FFF5DB'))
$gold = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#D8B76D')),4
$green = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#166E62'))
$graphics.DrawArc($gold,22,9,20,26,180,180)
$graphics.FillRectangle($cream,14,23,36,31)
$arrow = [System.Drawing.Point[]]@([System.Drawing.Point]::new(28,30),[System.Drawing.Point]::new(36,30),[System.Drawing.Point]::new(36,39),[System.Drawing.Point]::new(43,39),[System.Drawing.Point]::new(32,50),[System.Drawing.Point]::new(21,39),[System.Drawing.Point]::new(28,39))
$graphics.FillPolygon($green,$arrow)
$stream = New-Object System.IO.MemoryStream
$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
$png = $stream.ToArray()
$output = [System.IO.File]::Create((Join-Path $PSScriptRoot '..\assets\store.ico'))
$writer = New-Object System.IO.BinaryWriter $output
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22); $writer.Write($png)
$writer.Dispose(); $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $cream.Dispose(); $gold.Dispose(); $green.Dispose()
