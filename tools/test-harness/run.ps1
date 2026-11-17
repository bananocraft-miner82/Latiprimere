<#
.SYNOPSIS
	Sets up the virtual printers and runs Latiprimere's end-to-end scenarios on Windows. Nothing prints on paper.

.DESCRIPTION
	1. Starts the CUPS test container (tools/virtual-printers) if Docker is running, unless -NoContainer.
	2. Uses the Windows test printers (tools/windows-test-printers) if they are installed. With -InstallWindowsPrinters,
	   installs them first; that needs an elevated PowerShell.
	3. Sets the LATIPRIMERE_TEST_* variables for this run only.
	4. Runs the console harness (tests/Latiprimere.TestHarness), or with -Xunit the xUnit host for both target
	   frameworks, and leaves a report in out/harness.

	With -Packages, the console harness is built against Latiprimere's .nupkg files, packed first with a version of
	their own, rather than its projects. With -Aot, it is published with Native AOT and run as a native program. Both
	can be used together.

	Run it from anywhere; paths are relative to the repository. See docs/test-harness-plan.md.

.PARAMETER Filter
	Scenario IDs, ID prefixes ending in * (such as 'cups.*') or areas (such as 'Lifecycle'). None runs everything.

.PARAMETER Framework
	The target framework the console harness runs on. Default net11.0.

.PARAMETER Xunit
	Runs the scenarios through the xUnit host (dotnet test) instead of the console harness.

.PARAMETER NoContainer
	Doesn't start the CUPS test container; the CUPS and direct IPP scenarios skip.

.PARAMETER InstallWindowsPrinters
	Installs the Windows test printers first. Needs an elevated PowerShell.

.PARAMETER Down
	Stops the CUPS test container afterwards.

.PARAMETER Packages
	Packs Latiprimere into out/harness-packages/<time> and runs the scenarios against those packages.

.PARAMETER Aot
	Publishes the console harness with Native AOT into out/harness-aot and runs the native program. Fails on any
	trimming or AOT warning. Needs the platform's native toolchain, as tools/aot-check does.

.EXAMPLE
	./tools/test-harness/run.ps1

.EXAMPLE
	./tools/test-harness/run.ps1 cups.* Lifecycle -Down

.EXAMPLE
	./tools/test-harness/run.ps1 -Packages -Aot
#>
param(
	[Parameter(Position = 0, ValueFromRemainingArguments = $true)]
	[string[]] $Filter = @(),
	[string] $Framework = 'net11.0',
	[switch] $Xunit,
	[switch] $NoContainer,
	[switch] $InstallWindowsPrinters,
	[switch] $Down,
	[switch] $Packages,
	[switch] $Aot
)

$ErrorActionPreference = 'Stop'
$repository = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$composeFile = Join-Path $repository 'tools\virtual-printers\compose.yaml'

# Every variable this script sets, put back as it was at the end
$names = @(
	'LATIPRIMERE_TEST_E2E',
	'LATIPRIMERE_TEST_CUPS_SERVER',
	'LATIPRIMERE_TEST_CUPS_OUTPUT',
	'LATIPRIMERE_TEST_CUPS_SETUP',
	'LATIPRIMERE_TEST_IPP_PRINTER',
	'LATIPRIMERE_TEST_WINDOWS_OUTPUT'
)
$previous = @{}
foreach ($name in $names)
{
	$previous[$name] = [Environment]::GetEnvironmentVariable($name)
	[Environment]::SetEnvironmentVariable($name, $null)
}

$hasDocker = $false
$exitCode = 1

try
{
	$env:LATIPRIMERE_TEST_E2E = '1'

	# The CUPS test container: CUPS queues on 631, ippeveprinter on 8631, job output in out/cups-output. Addresses
	# are 127.0.0.1, not localhost, because the container publishes on IPv4 only.
	if (-not $NoContainer -and (Get-Command docker -ErrorAction SilentlyContinue))
	{
		docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
		$hasDocker = $LASTEXITCODE -eq 0
	}

	if ($hasDocker)
	{
		# compose.yaml mounts LATIPRIMERE_TEST_CUPS_OUTPUT as the container's /output
		$env:LATIPRIMERE_TEST_CUPS_OUTPUT = Join-Path $repository 'out\cups-output'
		New-Item -ItemType Directory -Force -Path $env:LATIPRIMERE_TEST_CUPS_OUTPUT | Out-Null
		Write-Host '== Starting the CUPS test container'

		# After the host restarts, the container is left stopped, and starting it again reuses everything the last run
		# left in it. So a container that is stopped, or running but unhealthy, is made afresh from the image. Checked
		# with docker ps first, since docker inspect writes an error for a container that doesn't exist.
		$recreate = @()
		$exists = docker ps --all --filter 'name=^latiprimere-cups$' --format '{{.Names}}'

		if ($exists)
		{
			$state = (docker inspect latiprimere-cups --format '{{.State.Status}}{{if .State.Health}} {{.State.Health.Status}}{{end}}').Trim()

			if ($state -ne 'running healthy' -and $state -ne 'running starting')
			{
				Write-Host "== The test container is $state, as after a restart: making it afresh"
				$recreate = @('--force-recreate')
			}
		}

		docker compose -f $composeFile up -d --build --wait @recreate
		if ($LASTEXITCODE -ne 0) { throw 'The CUPS test container did not start.' }

		$env:LATIPRIMERE_TEST_CUPS_SERVER = '127.0.0.1:631'
		$env:LATIPRIMERE_TEST_IPP_PRINTER = 'ipp://127.0.0.1:8631/ipp/print'
	}
	elseif (-not $NoContainer)
	{
		Write-Warning 'Docker is not running, so the CUPS and direct IPP scenarios will skip.'
	}

	# The Windows spooler test printers write to file ports in one folder
	if ($InstallWindowsPrinters)
	{
		& (Join-Path $repository 'tools\windows-test-printers\Install-TestPrinters.ps1')
	}

	$rawPrinter = Get-Printer -Name 'Latiprimere Raw' -ErrorAction SilentlyContinue
	if ($rawPrinter)
	{
		$env:LATIPRIMERE_TEST_WINDOWS_OUTPUT = Split-Path -Parent $rawPrinter.PortName
	}
	else
	{
		Write-Host 'The Windows test printers are not installed, so their scenarios will skip (-InstallWindowsPrinters, elevated, adds them).'
	}

	if ($Xunit -and ($Packages -or $Aot))
	{
		throw '-Packages and -Aot run the console harness; leave out -Xunit.'
	}

	$harnessProject = Join-Path $repository 'tests\Latiprimere.TestHarness'
	$buildProperties = @()

	if ($Packages)
	{
		# A version of their own, so NuGet's cache can never hand back an older build of the same version
		$stamp = Get-Date -Format 'yyyyMMddHHmmss'
		$packageFolder = Join-Path $repository "out\harness-packages\$stamp"
		$prefix = ([regex]::Match((Get-Content (Join-Path $repository 'Directory.Build.props') -Raw), '<VersionPrefix>([^<]+)</VersionPrefix>')).Groups[1].Value
		$suffix = "preview.e2e.$stamp"
		$version = "$prefix-$suffix"

		Write-Host "== Packing Latiprimere $version into $packageFolder"
		foreach ($project in Get-ChildItem (Join-Path $repository 'src') -Filter '*.csproj' -Recurse)
		{
			dotnet pack $project.FullName --configuration Release --output $packageFolder "-p:VersionSuffix=$suffix" -nologo
			if ($LASTEXITCODE -ne 0) { throw "Packing $($project.Name) failed." }
		}

		# Latiprimere's packages from that folder, everything else from nuget.org as the repository's nuget.config has it
		$config = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
	<packageSources>
		<clear />
		<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
		<add key="latiprimere-packed" value="$packageFolder" />
	</packageSources>
	<packageSourceMapping>
		<clear />
		<packageSource key="nuget.org">
			<package pattern="*" />
		</packageSource>
		<packageSource key="latiprimere-packed">
			<package pattern="Latiprimere" />
			<package pattern="Latiprimere.*" />
		</packageSource>
	</packageSourceMapping>
</configuration>
"@
		Set-Content -Path (Join-Path $packageFolder 'nuget.config') -Value $config -Encoding utf8
		$buildProperties = @("-p:LatiprimereFromPackages=$packageFolder", "-p:LatiprimerePackageVersion=$version")
	}

	if ($Xunit)
	{
		Write-Host '== Running the scenarios with xUnit'
		dotnet test --project (Join-Path $repository 'tests\Latiprimere.EndToEnd.Tests')
	}
	elseif ($Aot)
	{
		# The samples run as programs, so they are built as usual first
		Write-Host "== Building the samples for $Framework"
		foreach ($sample in 'Latiprimere.Cli', 'Latiprimere.Examples')
		{
			dotnet build (Join-Path $repository "samples\$sample") --configuration Release --framework $Framework -nologo
			if ($LASTEXITCODE -ne 0) { throw "Building $sample failed." }
		}

		$aotFolder = Join-Path $repository "out\harness-aot\$Framework"
		Write-Host "== Publishing the harness with Native AOT into $aotFolder"
		$publishLog = dotnet publish $harnessProject --configuration Release --framework $Framework --use-current-runtime -p:PublishAot=true -p:TrimmerSingleWarn=false --output $aotFolder -nologo @buildProperties 2>&1
		$publishLog | Write-Host
		if ($LASTEXITCODE -ne 0) { throw 'Publishing the harness with Native AOT failed.' }

		# IL2xxx are trimming warnings, IL3xxx AOT ones; either means some code may not work once published
		$warnings = $publishLog | Select-String -Pattern 'warning IL[23]\d{3}'
		if ($warnings) { throw "Publishing the harness reported $(@($warnings).Count) trimming or AOT warnings." }

		Write-Host "== Running the scenarios as a native program"
		& (Join-Path $aotFolder 'Latiprimere.TestHarness.exe') run @Filter
	}
	elseif ($Packages)
	{
		Write-Host "== Building the harness against the packages"
		dotnet build $harnessProject --configuration Release --framework $Framework -nologo @buildProperties
		if ($LASTEXITCODE -ne 0) { throw 'Building the harness against the packages failed.' }

		Write-Host "== Running the scenarios on $Framework, from the packages"
		& (Join-Path $repository "out\packaged\bin\Latiprimere.TestHarness\Release\$Framework\Latiprimere.TestHarness.exe") run @Filter
	}
	else
	{
		Write-Host "== Running the scenarios on $Framework"
		dotnet run --project $harnessProject -f $Framework -- run @Filter
	}

	$exitCode = $LASTEXITCODE
}
finally
{
	foreach ($name in $names)
	{
		[Environment]::SetEnvironmentVariable($name, $previous[$name])
	}

	if ($Down -and $hasDocker)
	{
		Write-Host '== Stopping the CUPS test container'
		docker compose -f $composeFile down
	}
}

exit $exitCode
