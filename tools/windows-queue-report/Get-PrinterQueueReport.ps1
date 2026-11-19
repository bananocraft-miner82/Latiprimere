<#
.SYNOPSIS
	Records how Windows has set up each printer queue: its port, port monitor, driver, registry entries and Plug and
	Play device properties. It changes nothing.

.DESCRIPTION
	Latiprimere can send a Windows queue's PDF jobs straight to its printer over IPP, when it knows the printer's IPP
	address (ADR 0027). For a queue added by URL the port name is that address, but how Windows 11 names the ports of
	queues it sets up itself for driverless IPP printers isn't documented. This script collects what is needed to find
	out, for every queue or for the ones named:

	  - Get-Printer, Get-PrinterPort and Get-PrinterDriver, with every property;
	  - the queue's spooler registry key, and its port's key under the port monitor;
	  - the queue's Plug and Play device, and every device in the same container (the physical printer), with every
	    device property, which is where Windows may keep the printer's IPP address;
	  - the port monitors installed, and the Windows version.

	The report is written as JSON. It needs no administrator rights.

	The report can identify the printers and the network: it holds IP addresses, host names, device IDs and often
	serial numbers. Read it before sending it to anyone, and replace what you'd rather not share.

.PARAMETER PrinterName
	The queues to report on. All of them if left out.

.PARAMETER OutputPath
	Where to write the report. printer-queue-report.json in the current folder if left out.

.EXAMPLE
	.\Get-PrinterQueueReport.ps1

.EXAMPLE
	.\Get-PrinterQueueReport.ps1 -PrinterName 'HP ENVY 5640 series' -OutputPath "$env:TEMP\hp-queue.json"
#>
param(
	[string[]] $PrinterName,
	[string] $OutputPath = (Join-Path (Get-Location) 'printer-queue-report.json')
)

$ErrorActionPreference = 'Stop'
$PrintKey = 'HKLM:\SYSTEM\CurrentControlSet\Control\Print'

# Turns a value into something ConvertTo-Json writes readably: byte arrays become hexadecimal, other arrays are
# converted item by item, and CIM instances become their properties
function ConvertTo-ReportValue
{
	param($Value)

	if ($null -eq $Value)
	{
		return $null
	}

	if ($Value -is [byte[]])
	{
		return ($Value | ForEach-Object { $_.ToString('x2') }) -join ''
	}

	if ($Value -is [string] -or $Value -is [ValueType])
	{
		return $Value.ToString()
	}

	if ($Value -is [System.Collections.IEnumerable])
	{
		return @($Value | ForEach-Object { ConvertTo-ReportValue $_ })
	}

	if ($Value -is [Microsoft.Management.Infrastructure.CimInstance])
	{
		return ConvertTo-PropertyTable $Value
	}

	return $Value.ToString()
}

# Every property of an object, by name, with CIM's own bookkeeping left out
function ConvertTo-PropertyTable
{
	param($InputObject)

	$table = [ordered]@{}

	foreach ($property in $InputObject.PSObject.Properties)
	{
		if ($property.Name -in 'CimClass', 'CimInstanceProperties', 'CimSystemProperties', 'PSComputerName')
		{
			continue
		}

		try
		{
			$table[$property.Name] = ConvertTo-ReportValue $property.Value
		}
		catch
		{
			$table[$property.Name] = "<unreadable: $($_.Exception.Message)>"
		}
	}

	return $table
}

# A registry key's values, and its subkeys' values one level down, or $null if it doesn't exist
function Get-RegistryEntries
{
	param([string] $Path)

	if (-not (Test-Path -LiteralPath $Path))
	{
		return $null
	}

	$entries = [ordered]@{}
	$key = Get-Item -LiteralPath $Path

	foreach ($name in $key.GetValueNames())
	{
		$entries[$(if ($name) { $name } else { '(default)' })] = ConvertTo-ReportValue $key.GetValue($name, $null, 'DoNotExpandEnvironmentNames')
	}

	foreach ($subkey in Get-ChildItem -LiteralPath $Path -ErrorAction SilentlyContinue)
	{
		$values = [ordered]@{}

		foreach ($name in $subkey.GetValueNames())
		{
			$values[$(if ($name) { $name } else { '(default)' })] = ConvertTo-ReportValue $subkey.GetValue($name, $null, 'DoNotExpandEnvironmentNames')
		}

		$entries["[$($subkey.PSChildName)]"] = $values
	}

	return $entries
}

# A Plug and Play device with all its properties
function Get-DeviceReport
{
	param([string] $InstanceId)

	$device = Get-PnpDevice -InstanceId $InstanceId -ErrorAction SilentlyContinue

	if ($null -eq $device)
	{
		return $null
	}

	$properties = [ordered]@{}

	foreach ($property in Get-PnpDeviceProperty -InstanceId $InstanceId -ErrorAction SilentlyContinue)
	{
		$name = if ($property.KeyName) { $property.KeyName } else { $property.Key }
		$properties[$name] = ConvertTo-ReportValue $property.Data
	}

	return [ordered]@{
		InstanceId   = $device.InstanceId
		FriendlyName = $device.FriendlyName
		Class        = $device.Class
		Status       = $device.Status
		Properties   = $properties
	}
}

# Every present device's container ID, read once in a single call: asking device by device takes minutes
$script:ContainerIds = $null

function Get-ContainerIds
{
	if ($null -eq $script:ContainerIds)
	{
		$ids = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | ForEach-Object InstanceId)
		$script:ContainerIds = @{}

		if ($ids.Count -gt 0)
		{
			foreach ($property in Get-PnpDeviceProperty -InstanceId $ids -KeyName 'DEVPKEY_Device_ContainerId' -ErrorAction SilentlyContinue)
			{
				if ($property.Data)
				{
					$script:ContainerIds[$property.InstanceId] = $property.Data.ToString()
				}
			}
		}
	}

	return $script:ContainerIds
}

# The devices in the same container as a print queue's device: the physical printer and its functions. Every device
# of the PC itself shares the null container, so that one is left out.
function Get-ContainerDevices
{
	param([string] $ContainerId, [string] $ExceptInstanceId)

	if (-not $ContainerId -or $ContainerId -match '^\{?0{8}-0{4}-0{4}-0{4}-0{12}\}?$' -or $ContainerId -match '^\{?00000000-0000-0000-ffff-ffffffffffff\}?$')
	{
		return @()
	}

	$containerIds = Get-ContainerIds
	$instanceIds = @($containerIds.Keys | Where-Object { $_ -ne $ExceptInstanceId -and $containerIds[$_] -eq $ContainerId })

	return @($instanceIds | ForEach-Object { Get-DeviceReport $_ })
}

# Every queue, briefly: Get-Printer without -Full doesn't ask print servers or drivers, so it doesn't fail for one
# that's offline. Names are matched exactly: Get-Printer -Name would read [ and ] in a name as a wildcard pattern.
$allPrinters = @(Get-Printer)
$printers = $allPrinters

if ($PrinterName)
{
	$printers = @($allPrinters | Where-Object { $PrinterName -contains $_.Name })

	foreach ($missing in $PrinterName | Where-Object { $allPrinters.Name -notcontains $_ })
	{
		Write-Warning "No printer queue is named '$missing'."
	}
}

# Print queues appear as Plug and Play devices of class PrintQueue, named after the queue
$queueDevices = @(Get-PnpDevice -Class PrintQueue -ErrorAction SilentlyContinue)

# Reads one queue in full
function Get-QueueReport
{
	param($Printer)

	# -Full adds the driver's settings, and asks a print server for a shared queue, so it can fail on its own
	$full = $Printer
	$fullError = $null

	try
	{
		$full = Get-Printer -Name ([System.Management.Automation.WildcardPattern]::Escape($Printer.Name)) -Full | Select-Object -First 1

		if ($null -eq $full)
		{
			$full = $Printer
		}
	}
	catch
	{
		$fullError = $_.Exception.Message
	}

	$port = Get-PrinterPort -Name $Printer.PortName -ErrorAction SilentlyContinue
	$driver = Get-PrinterDriver -Name $Printer.DriverName -ErrorAction SilentlyContinue
	$monitorName = if ($port) { $port.PortMonitor } else { $null }
	$queueDevice = $queueDevices | Where-Object { $_.FriendlyName -eq $Printer.Name } | Select-Object -First 1
	$deviceReport = if ($queueDevice) { Get-DeviceReport $queueDevice.InstanceId } else { $null }
	$containerId = if ($deviceReport) { $deviceReport.Properties['DEVPKEY_Device_ContainerId'] } else { $null }

	# A port monitor's ports are usually under its key, by port name; the monitor key is named after the monitor
	$portKey = $null

	if ($monitorName)
	{
		foreach ($monitorKey in Get-ChildItem -LiteralPath "$PrintKey\Monitors" -ErrorAction SilentlyContinue)
		{
			$candidate = Join-Path $monitorKey.PSPath "Ports\$($Printer.PortName)"

			if (Test-Path -LiteralPath $candidate)
			{
				$portKey = [ordered]@{ Monitor = $monitorKey.PSChildName; Entries = Get-RegistryEntries $candidate }
				break
			}
		}
	}

	return [ordered]@{
		Name             = $Printer.Name
		Printer          = ConvertTo-PropertyTable $full
		FullReadError    = $fullError
		Port             = if ($port) { ConvertTo-PropertyTable $port } else { $null }
		Driver           = if ($driver) { ConvertTo-PropertyTable $driver } else { $null }
		QueueRegistry    = Get-RegistryEntries "$PrintKey\Printers\$($Printer.Name)"
		PortRegistry     = $portKey
		QueueDevice      = $deviceReport
		ContainerDevices = Get-ContainerDevices $containerId $(if ($queueDevice) { $queueDevice.InstanceId } else { '' })
	}
}

# A queue that can't be read is recorded with the reason, and the report goes on with the others
$queues = foreach ($printer in $printers)
{
	Write-Host "Reading $($printer.Name)"

	try
	{
		Get-QueueReport $printer
	}
	catch
	{
		Write-Warning "Couldn't read $($printer.Name): $($_.Exception.Message)"
		[ordered]@{ Name = $printer.Name; Error = $_.Exception.Message; Printer = ConvertTo-PropertyTable $printer }
	}
}

$os = Get-CimInstance Win32_OperatingSystem

$report = [ordered]@{
	Tool         = 'tools/windows-queue-report/Get-PrinterQueueReport.ps1'
	Created      = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
	Windows      = "$($os.Caption) $($os.Version), build $($os.BuildNumber)"
	PortMonitors = @(Get-ChildItem -LiteralPath "$PrintKey\Monitors" -ErrorAction SilentlyContinue | ForEach-Object {
		[ordered]@{ Name = $_.PSChildName; Driver = (Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue).Driver }
	})
	Queues       = @($queues)
}

# UTF-8 without a byte order mark, which Windows PowerShell 5.1's Out-File can't write
$json = $report | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($OutputPath, $json, (New-Object System.Text.UTF8Encoding $false))

Write-Host "Wrote ${OutputPath}: $(@($queues).Count) queue(s)."
Write-Host 'It can identify your printers and network (addresses, device IDs, serial numbers): read it before sharing it.'
