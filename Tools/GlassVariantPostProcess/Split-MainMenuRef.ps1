param(
    [string]$SourcePath = "W:\Unity\FreelanceProjects\WindowFracture\Assets\2D\UI\GlassVariant\Ref\main_menu_mental_static_ref.png",
    [string]$OutputRoot = "W:\Unity\FreelanceProjects\WindowFracture\Assets\2D\UI\GlassVariant\MainMenu",
    [int]$DarkThreshold = 28,
    [int]$WhiteKeepThreshold = 145,
    [int]$Padding = 8
)

Add-Type -AssemblyName System.Drawing

function Save-Bitmap([System.Drawing.Bitmap]$bitmap, [string]$path) {
    $dir = Split-Path $path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Copy-Region([System.Drawing.Bitmap]$source, [int]$x, [int]$y, [int]$w, [int]$h) {
    $crop = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($source, (New-Object System.Drawing.Rectangle 0, 0, $w, $h), (New-Object System.Drawing.Rectangle $x, $y, $w, $h), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    return $crop
}

function Remove-DarkPixels([System.Drawing.Bitmap]$bitmap, [int]$threshold) {
    for ($y = 0; $y -lt $bitmap.Height; $y++) {
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            $c = $bitmap.GetPixel($x, $y)
            if ($c.A -eq 0) { continue }
            $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
            if ($max -le $threshold) {
                $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            }
        }
    }
}

function Keep-BrightPixels([System.Drawing.Bitmap]$bitmap, [int]$threshold) {
    for ($y = 0; $y -lt $bitmap.Height; $y++) {
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            $c = $bitmap.GetPixel($x, $y)
            if ($c.A -eq 0) { continue }
            $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
            $avg = ($c.R + $c.G + $c.B) / 3.0
            if ($max -lt $threshold -and $avg -lt ($threshold + 20)) {
                $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            }
        }
    }
}

function Trim-AlphaBounds([System.Drawing.Bitmap]$bitmap, [int]$pad) {
    $minX = $bitmap.Width; $minY = $bitmap.Height; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $bitmap.Height; $y++) {
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            if ($bitmap.GetPixel($x, $y).A -gt 0) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt 0) { return $bitmap }
    $cropX = [Math]::Max(0, $minX - $pad)
    $cropY = [Math]::Max(0, $minY - $pad)
    $cropW = [Math]::Min($bitmap.Width - $cropX, ($maxX - $minX + 1) + ($pad * 2))
    $cropH = [Math]::Min($bitmap.Height - $cropY, ($maxY - $minY + 1) + ($pad * 2))
    return Copy-Region $bitmap $cropX $cropY $cropW $cropH
}

function Export-Crop(
    [System.Drawing.Bitmap]$source,
    [string]$path,
    [int]$x, [int]$y, [int]$w, [int]$h,
    [ValidateSet('none', 'dark', 'bright')][string]$KeyMode = 'none'
) {
    $crop = Copy-Region $source $x $y $w $h
    switch ($KeyMode) {
        'dark' { Remove-DarkPixels $crop $DarkThreshold }
        'bright' { Keep-BrightPixels $crop $WhiteKeepThreshold }
    }
    $trimmed = Trim-AlphaBounds $crop $Padding
    if ($trimmed -ne $crop) { $crop.Dispose() }
    Save-Bitmap $trimmed $path
    $result = [PSCustomObject]@{ Path = $path; Width = $trimmed.Width; Height = $trimmed.Height }
    $trimmed.Dispose()
    return $result
}

function Build-Background([System.Drawing.Bitmap]$source, [string]$path, [array]$MaskRects) {
    $bg = New-Object System.Drawing.Bitmap $source.Width, $source.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bg)
    $g.DrawImage($source, 0, 0)

    $samples = @()
    foreach ($pt in @([System.Drawing.Point]::new(20, 20), [System.Drawing.Point]::new($source.Width - 21, 20), [System.Drawing.Point]::new(20, $source.Height - 21))) {
        $samples += $source.GetPixel($pt.X, $pt.Y)
    }
    $avgR = [int](($samples | ForEach-Object { $_.R } | Measure-Object -Average).Average)
    $avgG = [int](($samples | ForEach-Object { $_.G } | Measure-Object -Average).Average)
    $avgB = [int](($samples | ForEach-Object { $_.B } | Measure-Object -Average).Average)
    $fill = [System.Drawing.Color]::FromArgb(255, $avgR, $avgG, $avgB)
    $brush = New-Object System.Drawing.SolidBrush $fill

    foreach ($rect in $MaskRects) {
        $g.FillRectangle($brush, $rect)
    }

    $g.Dispose(); $brush.Dispose()
    Save-Bitmap $bg $path
    $bg.Dispose()
}

$sourcePathFull = [System.IO.Path]::GetFullPath($SourcePath)
if (-not (Test-Path $sourcePathFull)) { throw "Source not found: $sourcePathFull" }

$source = [System.Drawing.Bitmap]::FromFile($sourcePathFull)
$w = $source.Width
$h = $source.Height

# Normalized crop helpers for 768x1375 reference layout
$results = @()

# --- Background: full screen with UI regions masked to sampled backdrop ---
$maskRects = @(
    (New-Object System.Drawing.Rectangle ([int]($w * 0.04), [int]($h * 0.03), [int]($w * 0.92), [int]($h * 0.17))),   # title block
    (New-Object System.Drawing.Rectangle ([int]($w * 0.04), [int]($h * 0.22), [int]($w * 0.92), [int]($h * 0.075))),  # pill 1
    (New-Object System.Drawing.Rectangle ([int]($w * 0.04), [int]($h * 0.31), [int]($w * 0.92), [int]($h * 0.075))),  # pill 2
    (New-Object System.Drawing.Rectangle ([int]($w * 0.04), [int]($h * 0.40), [int]($w * 0.92), [int]($h * 0.075))), # pill 3
    (New-Object System.Drawing.Rectangle ([int]($w * 0.04), [int]($h * 0.52), [int]($w * 0.92), [int]($h * 0.13)))   # play
)
Build-Background $source (Join-Path $OutputRoot "BG\bg_main_menu.png") $maskRects
$results += [PSCustomObject]@{ Path = "BG/bg_main_menu.png"; Width = $w; Height = $h }

# --- Text layers (bright-keyed for isolated typography) ---
$textCrops = @(
    @{ Name = "text_mental.png"; X = 0.08; Y = 0.04; W = 0.84; H = 0.07 }
    @{ Name = "text_static.png"; X = 0.12; Y = 0.11; W = 0.76; H = 0.045 }
    @{ Name = "text_subtitle_3scene.png"; X = 0.10; Y = 0.155; W = 0.80; H = 0.035 }
    @{ Name = "text_anxiety.png"; X = 0.22; Y = 0.225; W = 0.50; H = 0.035 }
    @{ Name = "text_the_build_up.png"; X = 0.22; Y = 0.255; W = 0.50; H = 0.028 }
    @{ Name = "text_filter.png"; X = 0.22; Y = 0.315; W = 0.50; H = 0.035 }
    @{ Name = "text_the_impact.png"; X = 0.22; Y = 0.345; W = 0.50; H = 0.028 }
    @{ Name = "text_deadline.png"; X = 0.22; Y = 0.405; W = 0.50; H = 0.035 }
    @{ Name = "text_the_climax.png"; X = 0.22; Y = 0.435; W = 0.50; H = 0.028 }
    @{ Name = "text_play.png"; X = 0.30; Y = 0.545; W = 0.40; H = 0.08 }
)
foreach ($item in $textCrops) {
    $results += Export-Crop $source (Join-Path $OutputRoot "Text\$($item.Name)") `
        ([int]($w * $item.X)) ([int]($h * $item.Y)) ([int]($w * $item.W)) ([int]($h * $item.H)) 'bright'
}

# --- Icon layers (keep circles + painted icons, dark-key edges) ---
$iconCrops = @(
    @{ Name = "icon_num_01.png"; X = 0.06; Y = 0.228; W = 0.12; H = 0.065 }
    @{ Name = "icon_fist.png"; X = 0.78; Y = 0.228; W = 0.14; H = 0.065 }
    @{ Name = "icon_num_02.png"; X = 0.06; Y = 0.318; W = 0.12; H = 0.065 }
    @{ Name = "icon_aperture.png"; X = 0.78; Y = 0.318; W = 0.14; H = 0.065 }
    @{ Name = "icon_num_03.png"; X = 0.06; Y = 0.408; W = 0.12; H = 0.065 }
    @{ Name = "icon_clock.png"; X = 0.78; Y = 0.408; W = 0.14; H = 0.065 }
    @{ Name = "icon_x_star.png"; X = 0.08; Y = 0.545; W = 0.14; H = 0.08 }
    @{ Name = "icon_skull.png"; X = 0.78; Y = 0.545; W = 0.14; H = 0.08 }
)
foreach ($item in $iconCrops) {
    $results += Export-Crop $source (Join-Path $OutputRoot "Icons\$($item.Name)") `
        ([int]($w * $item.X)) ([int]($h * $item.Y)) ([int]($w * $item.W)) ([int]($h * $item.H)) 'dark'
}

# --- Panel shells (glass pills + play slab, for button backgrounds) ---
$panelCrops = @(
    @{ Name = "panel_scene_anxiety_glass.png"; X = 0.04; Y = 0.22; W = 0.92; H = 0.075 }
    @{ Name = "panel_scene_filter_glass.png"; X = 0.04; Y = 0.31; W = 0.92; H = 0.075 }
    @{ Name = "panel_scene_deadline_glass.png"; X = 0.04; Y = 0.40; W = 0.92; H = 0.075 }
    @{ Name = "btn_play_glass.png"; X = 0.04; Y = 0.52; W = 0.92; H = 0.13 }
)
foreach ($item in $panelCrops) {
    $results += Export-Crop $source (Join-Path $OutputRoot "Panels\$($item.Name)") `
        ([int]($w * $item.X)) ([int]($h * $item.Y)) ([int]($w * $item.W)) ([int]($h * $item.H)) 'dark'
}

$source.Dispose()
$results | Format-Table -AutoSize
Write-Output "Saved $($results.Count) assets under $OutputRoot"
