#Requires -RunAsAdministrator
<#
.SYNOPSIS
	Removes the printers and ports created by Install-TestPrinters.ps1.

.PARAMETER OutputDirectory
	The directory the file ports wrote to. Its contents are deleted only with -DeleteOutput.

.PARAMETER DeleteOutput
	Also delete the output directory and the files in it.

.PARAMETER RemoveDriver
	Also remove the "Generic / Text Only" driver. Leave it if other printers use it.
#>
param(
	[string] $OutputDirectory = (Join-Path $env:ProgramData 'Latiprimere\TestOutput'),
	[switch] $DeleteOutput,
	[switch] $RemoveDriver
)

$ErrorActionPreference = 'Stop'

foreach ($name in 'Latiprimere Raw', 'Latiprimere Retained', 'Latiprimere Paused')
{
	if (Get-Printer -Name $name -ErrorAction SilentlyContinue)
	{
		Remove-Printer -Name $name
		Write-Host "Removed '$name'."
	}
}

# Only the file ports belong to Latiprimere; nul: is a standard Windows port and stays
foreach ($file in 'Latiprimere-Raw.prn', 'Latiprimere-Retained.prn')
{
	$portName = Join-Path $OutputDirectory $file
	if (Get-PrinterPort -Name $portName -ErrorAction SilentlyContinue)
	{
		Remove-PrinterPort -Name $portName
		Write-Host "Removed port '$portName'."
	}
}

if ($RemoveDriver -and (Get-PrinterDriver -Name 'Generic / Text Only' -ErrorAction SilentlyContinue))
{
	Remove-PrinterDriver -Name 'Generic / Text Only'
	Write-Host 'Removed driver Generic / Text Only.'
}

if ($DeleteOutput -and (Test-Path $OutputDirectory))
{
	Remove-Item -Recurse -Force $OutputDirectory
	Write-Host "Deleted $OutputDirectory."
}
