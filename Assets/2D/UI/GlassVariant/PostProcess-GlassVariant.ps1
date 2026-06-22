param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [string]$OutputPath = $InputPath,
    [int]$BgThreshold = 12,
    [int]$Padding = 12
)

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")
$fastProcessor = Join-Path $projectRoot "Tools\GlassVariantPostProcess\GlassVariantPostProcess.exe"
if (-not (Test-Path $fastProcessor)) {
    $fastProcessor = Join-Path $PSScriptRoot "GlassVariantPostProcess.exe"
}
if (Test-Path $fastProcessor) {
    & $fastProcessor $InputPath $OutputPath $BgThreshold $Padding
    if ($LASTEXITCODE -ne 0) { throw "GlassVariantPostProcess.exe failed with exit code $LASTEXITCODE" }
    return
}

Add-Type -AssemblyName System.Drawing

function Get-PixelAverage([System.Drawing.Color]$color) {
    return ($color.R + $color.G + $color.B) / 3.0
}

function Get-PixelSaturationSpread([System.Drawing.Color]$color) {
    $max = [Math]::Max($color.R, [Math]::Max($color.G, $color.B))
    $min = [Math]::Min($color.R, [Math]::Min($color.G, $color.B))
    return $max - $min
}

function Test-IsBackdropPixel([System.Drawing.Color]$color, [int]$threshold) {
    if ($color.A -eq 0) { return $false }
    if ($color.R -le $threshold -and $color.G -le $threshold -and $color.B -le $threshold) { return $true }
    $average = Get-PixelAverage $color
    $spread = Get-PixelSaturationSpread $color
    if ($spread -le 18 -and $average -ge 95 -and $average -le 175) { return $true }
    return $spread -le 10 -and $average -ge 175
}

function Remove-EdgeBackdrop([System.Drawing.Bitmap]$bitmap, [int]$threshold) {
    $width = $bitmap.Width; $height = $bitmap.Height
    $visited = New-Object 'bool[]' ($width * $height)
    $queue = [System.Collections.Generic.Queue[System.Drawing.Point]]::new()
    for ($x = 0; $x -lt $width; $x++) { $queue.Enqueue([System.Drawing.Point]::new($x, 0)); $queue.Enqueue([System.Drawing.Point]::new($x, $height - 1)) }
    for ($y = 0; $y -lt $height; $y++) { $queue.Enqueue([System.Drawing.Point]::new(0, $y)); $queue.Enqueue([System.Drawing.Point]::new($width - 1, $y)) }
    $removed = 0
    while ($queue.Count -gt 0) {
        $point = $queue.Dequeue()
        if ($point.X -lt 0 -or $point.Y -lt 0 -or $point.X -ge $width -or $point.Y -ge $height) { continue }
        $index = $point.Y * $width + $point.X
        if ($visited[$index]) { continue }
        $visited[$index] = $true
        $color = $bitmap.GetPixel($point.X, $point.Y)
        if ($color.A -eq 0) {
            $queue.Enqueue([System.Drawing.Point]::new($point.X - 1, $point.Y))
            $queue.Enqueue([System.Drawing.Point]::new($point.X + 1, $point.Y))
            $queue.Enqueue([System.Drawing.Point]::new($point.X, $point.Y - 1))
            $queue.Enqueue([System.Drawing.Point]::new($point.X, $point.Y + 1))
            continue
        }
        if (-not (Test-IsBackdropPixel $color $threshold)) { continue }
        $bitmap.SetPixel($point.X, $point.Y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
        $removed++
        $queue.Enqueue([System.Drawing.Point]::new($point.X - 1, $point.Y))
        $queue.Enqueue([System.Drawing.Point]::new($point.X + 1, $point.Y))
        $queue.Enqueue([System.Drawing.Point]::new($point.X, $point.Y - 1))
        $queue.Enqueue([System.Drawing.Point]::new($point.X, $point.Y + 1))
    }
    return $removed
}

function Get-AlphaBounds([System.Drawing.Bitmap]$bitmap) {
    $width = $bitmap.Width; $height = $bitmap.Height
    $minX = $width; $minY = $height; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            if ($bitmap.GetPixel($x, $y).A -gt 0) {
                if ($x -lt $minX) { $minX = $x }; if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt 0) { throw "No opaque pixels found in image." }
    return @{ MinX = $minX; MinY = $minY; MaxX = $maxX; MaxY = $maxY }
}

$inputFullPath = [System.IO.Path]::GetFullPath($InputPath)
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path $inputFullPath)) { throw "Input file not found: $inputFullPath" }

$source = [System.Drawing.Bitmap]::FromFile($inputFullPath)
$cropped = $null
try {
    $format32Argb = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    $working = New-Object System.Drawing.Bitmap $source.Width, $source.Height, $format32Argb
    $graphics = [System.Drawing.Graphics]::FromImage($working)
    $graphics.DrawImage($source, 0, 0, $source.Width, $source.Height)
    $graphics.Dispose(); $source.Dispose(); $source = $null

    $removed = Remove-EdgeBackdrop $working $BgThreshold
    $bounds = Get-AlphaBounds $working
    $cropX = [Math]::Max(0, $bounds.MinX - $Padding)
    $cropY = [Math]::Max(0, $bounds.MinY - $Padding)
    $cropWidth = [Math]::Min($working.Width - $cropX, ($bounds.MaxX - $bounds.MinX + 1) + ($Padding * 2))
    $cropHeight = [Math]::Min($working.Height - $cropY, ($bounds.MaxY - $bounds.MinY + 1) + ($Padding * 2))

    $cropped = New-Object System.Drawing.Bitmap $cropWidth, $cropHeight, $format32Argb
    $cropGraphics = [System.Drawing.Graphics]::FromImage($cropped)
    $cropGraphics.Clear([System.Drawing.Color]::Transparent)
    $cropGraphics.DrawImage($working, (New-Object System.Drawing.Rectangle 0, 0, $cropWidth, $cropHeight), (New-Object System.Drawing.Rectangle $cropX, $cropY, $cropWidth, $cropHeight), [System.Drawing.GraphicsUnit]::Pixel)
    $cropGraphics.Dispose(); $working.Dispose()

    $tempOutputPath = [System.IO.Path]::Combine((Split-Path $outputFullPath -Parent), ([System.IO.Path]::GetFileNameWithoutExtension($outputFullPath) + ".tmp.png"))
    if (Test-Path $tempOutputPath) { Remove-Item $tempOutputPath -Force }
    $cropped.Save($tempOutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $corners = @($cropped.GetPixel(0,0).A, $cropped.GetPixel($cropWidth-1,0).A, $cropped.GetPixel(0,$cropHeight-1).A, $cropped.GetPixel($cropWidth-1,$cropHeight-1).A)
    $cropped.Dispose(); $cropped = $null
    if (Test-Path $outputFullPath) { Remove-Item $outputFullPath -Force -ErrorAction SilentlyContinue }
    Move-Item -Path $tempOutputPath -Destination $outputFullPath -Force

    [PSCustomObject]@{
        Input = $inputFullPath; Output = $outputFullPath; RemovedBackdropPixels = $removed
        Width = $cropWidth; Height = $cropHeight; CornerAlpha = ($corners -join ',')
        BorderTransparent = (($corners | Where-Object { $_ -eq 0 }).Count -eq 4)
    }
}
finally {
    if ($source) { $source.Dispose() }
    if ($cropped) { $cropped.Dispose() }
}
