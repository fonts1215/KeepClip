param(
    [string]$Source = (Join-Path $PSScriptRoot '../branding/KeepClip.master.png'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../src/ClipShelf.App/Assets')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourceImage = [System.Drawing.Bitmap]::new([IO.Path]::GetFullPath($Source))
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
function Write-Png([string]$Name, [int]$Size) {
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        $graphics.DrawImage($sourceImage, [System.Drawing.Rectangle]::new(0, 0, $Size, $Size), 0, 0, $sourceImage.Width, $sourceImage.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
        $bitmap.Save((Join-Path $outputRoot $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $attributes.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
try {
    Write-Png 'AppLogo.png' 256
    foreach ($logo in @(@('StoreLogo', 50), @('Square44x44Logo', 44), @('Square150x150Logo', 150))) {
        Write-Png "$($logo[0]).png" ([int]$logo[1])
        foreach ($scale in @(100, 125, 150, 200, 250, 300, 400)) {
            Write-Png "$($logo[0]).scale-$scale.png" ([int][Math]::Ceiling([int]$logo[1] * $scale / 100))
        }
    }
    $iconSizes = @(16, 20, 24, 32, 40, 48, 64, 96, 256)
    foreach ($size in $iconSizes) {
        foreach ($suffix in @('', '_altform-unplated', '_altform-lightunplated')) {
            Write-Png "Square44x44Logo.targetsize-$size$suffix.png" $size
        }
    }
    # ICO frames contain the same transparent PNGs, with no palette or alpha conversion.
    $frames = @($iconSizes | ForEach-Object { ,([IO.File]::ReadAllBytes((Join-Path $outputRoot "Square44x44Logo.targetsize-$_.png"))) })
    $stream = [IO.File]::Create((Join-Path $outputRoot 'KeepClip.ico'))
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$iconSizes.Count)
        $offset = 6 + 16 * $iconSizes.Count
        for ($i = 0; $i -lt $iconSizes.Count; $i++) {
            $encodedSize = if ($iconSizes[$i] -eq 256) { 0 } else { $iconSizes[$i] }
            $writer.Write([byte]$encodedSize); $writer.Write([byte]$encodedSize)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose(); $stream.Dispose() }
    Write-Output "Icon assets: $outputRoot"
} finally { $sourceImage.Dispose() }
