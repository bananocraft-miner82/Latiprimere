<#
.SYNOPSIS
	Writes photo-300x200.jpg from photo-300x200.png, on Windows, with System.Drawing.

.DESCRIPTION
	The PNG's transparent disc is drawn over white first, as JPEG has no transparency. Run make-images.py first, then
	this, from this folder.
#>

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$folder = $PSScriptRoot
$png = [System.Drawing.Image]::FromFile((Join-Path $folder 'photo-300x200.png'))

try
{
	$bitmap = New-Object System.Drawing.Bitmap $png.Width, $png.Height
	$bitmap.SetResolution(96, 96)
	$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
	$graphics.Clear([System.Drawing.Color]::White)
	$graphics.DrawImage($png, 0, 0, $png.Width, $png.Height)
	$graphics.Dispose()

	$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
	$parameters = New-Object System.Drawing.Imaging.EncoderParameters 1
	$parameters.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 90L
	$bitmap.Save((Join-Path $folder 'photo-300x200.jpg'), $codec, $parameters)
	$bitmap.Dispose()
}
finally
{
	$png.Dispose()
}
