using Latiprimere.EndToEnd.Support;
using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Latiprimere.EndToEnd;

/// <summary>
/// The test printers this machine has, found from the integration tests' environment variables and, for Microsoft
/// Print to PDF, from the Windows spooler.
/// </summary>
public sealed class HarnessTargets
{
	/// <summary>
	/// Gets the CUPS server with the test queues, such as <c>127.0.0.1:631</c>, or <see langword="null"/>.
	/// </summary>
	public string? CupsServer { get; init; }

	/// <summary>
	/// Gets the folder where the CUPS test queues save their output, or <see langword="null"/>.
	/// </summary>
	public string? CupsOutputDirectory { get; init; }

	/// <summary>
	/// Gets which set of test queues the CUPS server has.
	/// </summary>
	public CupsTestSetup CupsSetup { get; init; }

	/// <summary>
	/// Gets the virtual IPP printer's URI, or <see langword="null"/>.
	/// </summary>
	public Uri? IppPrinterUri { get; init; }

	/// <summary>
	/// Gets the Windows test printers' output folder, or <see langword="null"/> when they aren't installed.
	/// </summary>
	public string? WindowsOutputDirectory { get; init; }

	/// <summary>
	/// Gets a value indicating whether Microsoft Print to PDF is installed.
	/// </summary>
	public bool HasMicrosoftPrintToPdf { get; init; }

	/// <summary>
	/// Gets a value indicating whether a person is at the keyboard to answer questions.
	/// </summary>
	public bool HasPerson { get; init; }

	/// <summary>
	/// Gets the printer a person named for manual checks, which may be real and print on paper, or
	/// <see langword="null"/> outside manual mode.
	/// </summary>
	public string? ManualPrinterId { get; init; }

	/// <summary>
	/// Gets the SHA-256 fingerprints of certificates to trust for the printer named for manual checks, when it is
	/// reached over IPPS; empty to trust only what the system does.
	/// </summary>
	public IReadOnlyList<string> ManualCertificateFingerprints { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Gets the DNS-SD instance name of an IPP printer announcing itself over mDNS, or <see langword="null"/>.
	/// </summary>
	public string? MdnsPrinterName { get; init; }

	/// <summary>
	/// Gets everything available here.
	/// </summary>
	public TargetNeeds Available
	{
		get
		{
			TargetNeeds available = TargetNeeds.None;

			if (this.CupsServer is not null)
			{
				available |= TargetNeeds.CupsServer;

				if (this.CupsSetup == CupsTestSetup.Container)
				{
					available |= TargetNeeds.CupsContainer;
				}
			}

			if (this.CupsOutputDirectory is not null && Directory.Exists(this.CupsOutputDirectory))
			{
				available |= TargetNeeds.CupsOutput;
			}

			if (this.IppPrinterUri is not null)
			{
				available |= TargetNeeds.IppPrinter;
			}

			if (this.WindowsOutputDirectory is not null && OperatingSystem.IsWindows())
			{
				available |= TargetNeeds.WindowsTestPrinters;
			}

			if (this.HasMicrosoftPrintToPdf)
			{
				available |= TargetNeeds.MicrosoftPrintToPdf;
			}

			if (this.HasPerson)
			{
				available |= TargetNeeds.Person;
			}

			if (this.MdnsPrinterName is not null)
			{
				available |= TargetNeeds.MdnsPrinter;
			}

			return available;
		}
	}

	/// <summary>
	/// Finds the test printers this machine has.
	/// </summary>
	/// <param name="hasPerson">Whether a person is at the keyboard to answer questions.</param>
	/// <param name="cancellationToken">Cancels the search.</param>
	/// <returns>The targets.</returns>
	/// <exception cref="InvalidOperationException">One of the integration tests' variables has a value it can't
	/// have.</exception>
	public static Task<HarnessTargets> DetectAsync(bool hasPerson, CancellationToken cancellationToken)
	{
		return HarnessTargets.DetectAsync(hasPerson, manualPrinterId: null, cancellationToken);
	}

	/// <summary>
	/// Finds the test printers this machine has, for manual mode: with a person at the keyboard, and the printer they
	/// named.
	/// </summary>
	/// <param name="hasPerson">Whether a person is at the keyboard to answer questions.</param>
	/// <param name="manualPrinterId">The printer named for manual checks, or <see langword="null"/>.</param>
	/// <param name="cancellationToken">Cancels the search.</param>
	/// <returns>The targets.</returns>
	public static async Task<HarnessTargets> DetectAsync(bool hasPerson, string? manualPrinterId, CancellationToken cancellationToken)
	{
		bool hasMicrosoftPrintToPdf = OperatingSystem.IsWindows()
			&& await HarnessTargets.HasWindowsPrinterAsync(TestPrinters.MicrosoftPrintToPdf, cancellationToken);

		return new HarnessTargets
		{
			CupsServer = IntegrationEnvironment.CupsServer,
			CupsOutputDirectory = IntegrationEnvironment.CupsOutputDirectory,
			CupsSetup = IntegrationEnvironment.CupsSetup,
			IppPrinterUri = IntegrationEnvironment.IppPrinterUri,
			WindowsOutputDirectory = IntegrationEnvironment.WindowsOutputDirectory,
			HasMicrosoftPrintToPdf = hasMicrosoftPrintToPdf,
			HasPerson = hasPerson,
			ManualPrinterId = manualPrinterId,
			MdnsPrinterName = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HarnessEnvironment.MdnsNameVariable))
				? null
				: Environment.GetEnvironmentVariable(HarnessEnvironment.MdnsNameVariable)!.Trim()
		};
	}

	/// <summary>
	/// Makes a copy of these targets that pins the given certificates for the printer named for manual checks.
	/// </summary>
	/// <param name="fingerprints">The SHA-256 fingerprints, as its web page shows them.</param>
	/// <returns>The copy.</returns>
	public HarnessTargets WithManualCertificateFingerprints(IReadOnlyList<string> fingerprints)
	{
		ArgumentNullException.ThrowIfNull(fingerprints);

		return new HarnessTargets
		{
			CupsServer                    = this.CupsServer,
			CupsOutputDirectory           = this.CupsOutputDirectory,
			CupsSetup                     = this.CupsSetup,
			IppPrinterUri                 = this.IppPrinterUri,
			WindowsOutputDirectory        = this.WindowsOutputDirectory,
			HasMicrosoftPrintToPdf        = this.HasMicrosoftPrintToPdf,
			HasPerson                     = this.HasPerson,
			ManualPrinterId               = this.ManualPrinterId,
			ManualCertificateFingerprints = fingerprints.ToArray(),
			MdnsPrinterName               = this.MdnsPrinterName
		};
	}

	/// <summary>
	/// Says why a scenario with these needs can't run here.
	/// </summary>
	/// <param name="needs">What the scenario needs.</param>
	/// <returns>One reason for each missing need; empty when the scenario can run.</returns>
	public IReadOnlyList<string> DescribeMissing(TargetNeeds needs)
	{
		TargetNeeds missing = needs & ~this.Available;
		List<string> reasons = new List<string>();

		if (missing.HasFlag(TargetNeeds.CupsServer))
		{
			reasons.Add($"no CUPS test server: set {IntegrationEnvironment.CupsServerVariable}");
		}

		if (missing.HasFlag(TargetNeeds.CupsOutput))
		{
			reasons.Add($"no CUPS output folder: set {IntegrationEnvironment.CupsOutputVariable} to an existing folder");
		}

		// The container is a CUPS server too: without any server, that's the reason, unless it was given just now
		if (missing.HasFlag(TargetNeeds.CupsContainer))
		{
			if (this.CupsServer is not null)
			{
				reasons.Add($"the CUPS server is a Mac's own CUPS ({IntegrationEnvironment.CupsSetupVariable}), without the container's queues");
			}
			else if (!missing.HasFlag(TargetNeeds.CupsServer))
			{
				reasons.Add($"no CUPS test server: set {IntegrationEnvironment.CupsServerVariable}");
			}
		}

		if (missing.HasFlag(TargetNeeds.IppPrinter))
		{
			reasons.Add($"no virtual IPP printer: set {IntegrationEnvironment.IppPrinterVariable}");
		}

		if (missing.HasFlag(TargetNeeds.WindowsTestPrinters))
		{
			reasons.Add($"no Windows test printers: run tools/windows-test-printers and set {IntegrationEnvironment.WindowsOutputVariable}");
		}

		if (missing.HasFlag(TargetNeeds.MicrosoftPrintToPdf))
		{
			reasons.Add("Microsoft Print to PDF isn't installed");
		}

		if (missing.HasFlag(TargetNeeds.Person))
		{
			reasons.Add("needs a person: run it in the console harness's manual mode");
		}

		if (missing.HasFlag(TargetNeeds.MdnsPrinter))
		{
			reasons.Add($"no printer announcing itself over mDNS: set {HarnessEnvironment.MdnsNameVariable} to its name");
		}

		return reasons;
	}

	/// <summary>
	/// Describes the targets, one line each, for reports.
	/// </summary>
	/// <returns>The lines.</returns>
	public IReadOnlyList<string> Describe()
	{
		return
		[
			$"CUPS server: {this.CupsServer ?? "none"}" + (this.CupsServer is null ? string.Empty : $" ({this.CupsSetup})"),
			$"CUPS output: {this.CupsOutputDirectory ?? "none"}",
			$"Virtual IPP printer: {this.IppPrinterUri?.AbsoluteUri ?? "none"}",
			$"Windows test printers: {this.WindowsOutputDirectory ?? "none"}",
			$"Microsoft Print to PDF: {(this.HasMicrosoftPrintToPdf ? "yes" : "no")}",
			$"Person at the keyboard: {(this.HasPerson ? "yes" : "no")}",
			$"Printer for manual checks: {this.ManualPrinterId ?? "none"}",
			$"Pinned certificates for it: {(this.ManualCertificateFingerprints.Count == 0 ? "none" : string.Join(", ", this.ManualCertificateFingerprints))}",
			$"Printer announced over mDNS: {this.MdnsPrinterName ?? "none"}",
			$"Latiprimere under test: {HarnessTargets.LatiprimereVersion}"
		];
	}

	/// <summary>
	/// Registers every print system the targets cover: CUPS and direct IPP when they are configured (with mDNS
	/// discovery off, so only the test printers are found), and the Windows spooler with PDFium on Windows.
	/// </summary>
	/// <param name="services">The service collection, with Latiprimere already added.</param>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	public void RegisterPrintSystems(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		if (this.CupsServer is not null)
		{
			string server = this.CupsServer;
			services.AddLatiprimereCups(options => options.Server = server);
		}

		if (this.IppPrinterUri is not null)
		{
			Uri printerUri = this.IppPrinterUri;

			services.AddLatiprimereIpp
			(
				options =>
				{
					options.EnableDiscovery = false;
					options.PrinterUris.Add(printerUri);
				}
			);
		}

		if (OperatingSystem.IsWindows())
		{
			services.AddLatiprimerePdfium();
			services.AddLatiprimereWindows();
		}
	}

	/// <summary>
	/// Gets the version of the Latiprimere assemblies the harness runs against, such as <c>0.1.1-preview+abc123</c>
	/// from a project build, or the version packed for packaged mode.
	/// </summary>
	public static string LatiprimereVersion
	{
		get
		{
			System.Reflection.Assembly assembly = typeof(PrinterCatalog).Assembly;
			string version = System.Reflection.CustomAttributeExtensions
				.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly)?.InformationalVersion ?? "unknown";

			return version;
		}
	}

	/// <summary>
	/// Says whether the Windows spooler has a printer.
	/// </summary>
	/// <param name="printerId">The printer's ID.</param>
	/// <param name="cancellationToken">Cancels the search.</param>
	/// <returns><see langword="true"/> if the spooler lists it.</returns>
	private static async Task<bool> HasWindowsPrinterAsync(string printerId, CancellationToken cancellationToken)
	{
		await using CatalogFixture fixture = new CatalogFixture(services => services.AddLatiprimereWindows());

		try
		{
			IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(cancellationToken);

			return printers.Any(printer => printer.Id == printerId);
		}
		catch (PrintException)
		{
			return false;
		}
	}
}
