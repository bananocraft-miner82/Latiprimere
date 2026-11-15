#Requires -RunAsAdministrator
<#
.SYNOPSIS
	Creates the Windows printers Latiprimere's spooler tests use. Nothing prints on paper.

.DESCRIPTION
	Installs the inbox "Generic / Text Only" driver (already in the Windows driver store) and creates:

	  Latiprimere Raw       Writes each job's bytes unchanged to <OutputDirectory>\Latiprimere-Raw.prn
	  Latiprimere Retained  Same, but keeps finished jobs in the queue ("Keep printed documents")
	  Latiprimere Paused    Paused queue on the nul: port, so jobs wait until cancelled

	Run Remove-TestPrinters.ps1 to undo. Re-running this script recreates the printers.

.PARAMETER OutputDirectory
	Where the file ports write. The print spooler (running as SYSTEM) must be able to write here.
#>
param(
	[string] $OutputDirectory = (Join-Path $env:ProgramData 'Latiprimere\TestOutput')
)

$ErrorActionPreference = 'Stop'
$DriverName = 'Generic / Text Only'
$Comment = 'Latiprimere test printer'

function Add-TestPrinter
{
	param([string] $Name, [string] $PortName)

	# Local ports named with a file path write each job to that file
	if (-not (Get-PrinterPort -Name $PortName -ErrorAction SilentlyContinue))
	{
		Add-PrinterPort -Name $PortName
	}

	# Recreate the printer so its settings are known
	if (Get-Printer -Name $Name -ErrorAction SilentlyContinue)
	{
		Remove-Printer -Name $Name
	}

	Add-Printer -Name $Name -DriverName $DriverName -PortName $PortName -Comment $Comment -Location 'This PC'
	Write-Host "Created '$Name' on port '$PortName'."
}

# The driver ships with Windows; installing it just adds it from the driver store
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
if (-not (Get-PrinterDriver -Name $DriverName -ErrorAction SilentlyContinue))
{
	Add-PrinterDriver -Name $DriverName
	Write-Host "Installed driver '$DriverName'."
}

Add-TestPrinter -Name 'Latiprimere Raw' -PortName (Join-Path $OutputDirectory 'Latiprimere-Raw.prn')
Add-TestPrinter -Name 'Latiprimere Retained' -PortName (Join-Path $OutputDirectory 'Latiprimere-Retained.prn')
Add-TestPrinter -Name 'Latiprimere Paused' -PortName 'nul:'

# Keep finished jobs on the Retained printer, so their final status can be read
& rundll32.exe printui.dll,PrintUIEntry /Xs /n 'Latiprimere Retained' attributes +KeepPrintedJobs
Start-Sleep -Seconds 1

# Pause the Paused printer so jobs stay in the queue
Get-CimInstance -ClassName Win32_Printer -Filter "Name='Latiprimere Paused'" |
	Invoke-CimMethod -MethodName Pause |
	Out-Null

Get-Printer -Name 'Latiprimere *' | Format-Table Name, DriverName, PortName, PrinterStatus -AutoSize
Write-Host "Output files are written to $OutputDirectory"
