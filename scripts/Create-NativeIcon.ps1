[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root 'src\Akara.Desktop\Assets\Art\akara-mark.png'
$destination = Join-Path $root 'assets\akara-native.ico'
$original = [Drawing.Bitmap]::new($source)
$frames = [Collections.Generic.List[byte[]]]::new()
$sizes = @(32,48,64,128,256)
try {
    # Build-format conversion only. Original art is kept intact and cropped at runtime.
    $bounds = [Drawing.Rectangle]::new(265,72,780,1007)
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $memory = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $height = [single]($size * 0.9)
            $width = [single]($height * $bounds.Width / $bounds.Height)
            $target = [Drawing.RectangleF]::new(($size-$width)/2,($size-$height)/2,$width,$height)
            $graphics.DrawImage($original,$target,$bounds,[Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($memory.ToArray())
        } finally { $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $original.Dispose() }
$output = [IO.File]::Create($destination)
$writer = [IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    for($index=0;$index-lt$frames.Count;$index++) {
        $size = $sizes[$index]
        $encodedSize = if($size-eq256){[byte]0}else{[byte]$size}
        $writer.Write($encodedSize); $writer.Write($encodedSize)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach($frame in $frames){$writer.Write($frame)}
} finally { $writer.Dispose(); $output.Dispose() }
Get-FileHash -LiteralPath $destination -Algorithm SHA256
