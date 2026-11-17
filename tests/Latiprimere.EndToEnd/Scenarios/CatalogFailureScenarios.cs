using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// The catalog when print systems fail, are missing, or don't know a printer (ADR 0008): every package registered
/// together, as an application does.
/// </summary>
public static class CatalogFailureScenarios
{
	/// <summary>
	/// One print system that can't be reached leaves the others working, and is logged.
	/// </summary>
	public static readonly Scenario OneSystemDown = new Scenario
	{
		Id = "catalog.one-system-down",
		Area = ScenarioArea.Catalog,
		Title = "With the CUPS server unreachable, the catalog still lists and finds the other print systems' printers, logs a warning, and reports CUPS printers as unreachable rather than missing",
		Needs = TargetNeeds.None,
		RunAsync = CatalogFailureScenarios.OneSystemDownAsync
	};

	/// <summary>
	/// When the only print system fails, its own error is what the caller sees.
	/// </summary>
	public static readonly Scenario EverySystemDown = new Scenario
	{
		Id = "catalog.every-system-down",
		Area = ScenarioArea.Catalog,
		Title = "With the only print system unreachable, listing printers and finding the default throw its own PrintException",
		Needs = TargetNeeds.None,
		RunAsync = CatalogFailureScenarios.EverySystemDownAsync
	};

	/// <summary>
	/// A catalog with no print system says printing isn't available.
	/// </summary>
	public static readonly Scenario NoPrintSystem = new Scenario
	{
		Id = "catalog.no-print-system",
		Area = ScenarioArea.Catalog,
		Title = "A catalog with no print system registered throws PrintSystemUnavailableException for listing, lookup and the default printer",
		Needs = TargetNeeds.None,
		RunAsync = CatalogFailureScenarios.NoPrintSystemAsync
	};

	/// <summary>
	/// IDs no print system knows are reported as not found, with the ID.
	/// </summary>
	public static readonly Scenario UnknownIds = new Scenario
	{
		Id = "catalog.unknown-ids",
		Area = ScenarioArea.Catalog,
		Title = "An unknown prefix, an IPP URI that isn't configured, and (on Windows) a queue that doesn't exist are each reported as PrinterNotFoundException with the ID",
		Needs = TargetNeeds.None,
		RunAsync = CatalogFailureScenarios.UnknownIdsAsync
	};

	/// <summary>
	/// Runs <see cref="OneSystemDown"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task OneSystemDownAsync(ScenarioContext context)
	{
		string closedServer = await CatalogFailureScenarios.GetClosedAddressAsync(context);
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		RecordingLogger<PrinterCatalog> logger = new RecordingLogger<PrinterCatalog>();

		await using CatalogFixture fixture = new CatalogFixture
		(
			services =>
			{
				services.AddSingleton<ILogger<PrinterCatalog>>(logger);
				services.AddLatiprimereCups(options => options.Server = closedServer);
				services.AddLatiprimereIpp(options =>
				{
					options.EnableDiscovery = false;
					options.PrinterUris.Add(fake.PrinterUri);
				});

				if (OperatingSystem.IsWindows())
				{
					services.AddLatiprimerePdfium().AddLatiprimereWindows();
				}
			}
		);

		string fakeId = FakePrinters.GetPrinterId(fake);
		IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(context.CancellationToken);
		context.Note("Listed: " + string.Join(", ", printers.Select(printer => printer.Id)));
		context.Check("the IPP printer is listed", printers.Any(printer => printer.Id == fakeId));
		context.Check("no CUPS printer is listed", !printers.Any(printer => printer.Id.StartsWith("cups:", StringComparison.Ordinal)));

		if (context.Targets.HasMicrosoftPrintToPdf)
		{
			context.Check("the Windows printers are listed", printers.Any(printer => printer.Id == TestPrinters.MicrosoftPrintToPdf));
		}

		IReadOnlyList<string> entries = logger.Entries;
		await context.SaveEvidenceAsync("catalog-log.txt", entries, "What the catalog logged");
		context.Check("a warning says CUPS was skipped", entries.Any(entry => entry.StartsWith("Warning:", StringComparison.Ordinal) && entry.Contains("CUPS", StringComparison.OrdinalIgnoreCase)), string.Join(" | ", entries.Where(entry => entry.StartsWith("Warning:", StringComparison.Ordinal))));

		IPrinter printer = await fixture.Catalog.GetPrinterAsync(fakeId, context.CancellationToken);
		context.CheckEqual("the IPP printer is found by its ID", fakeId, printer.Info.Id);

		PrintException? cupsLookup = null;

		try
		{
			await fixture.Catalog.GetPrinterAsync(TestPrinters.CupsRaw, context.CancellationToken);
		}
		catch (PrintException exception)
		{
			cupsLookup = exception;
		}

		context.Check("looking up a CUPS printer throws", cupsLookup is not null);
		context.Check("as unreachable, not as a missing printer", cupsLookup is not null and not PrinterNotFoundException, cupsLookup is null ? null : $"{cupsLookup.GetType().Name}: {cupsLookup.Message}");

		PrintException? defaultError = null;

		try
		{
			IPrinter? defaultPrinter = await fixture.Catalog.GetDefaultPrinterAsync(context.CancellationToken);
			context.Note($"Default printer: {defaultPrinter?.Info.Id ?? "none"}");
		}
		catch (PrintException exception)
		{
			defaultError = exception;
		}

		context.Check("finding the default printer doesn't throw", defaultError is null, defaultError?.Message);
	}

	/// <summary>
	/// Runs <see cref="EverySystemDown"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task EverySystemDownAsync(ScenarioContext context)
	{
		string closedServer = await CatalogFailureScenarios.GetClosedAddressAsync(context);
		await using CatalogFixture fixture = new CatalogFixture(services => services.AddLatiprimereCups(options => options.Server = closedServer));

		PrintException? listing = await CatalogFailureScenarios.CatchAsync(() => fixture.Catalog.GetPrintersAsync(context.CancellationToken));
		context.Require("listing printers throws a PrintException", listing is not null);
		context.Note($"{listing!.GetType().Name}: {listing.Message}");
		context.Check("it is CUPS's own error, naming the server", listing.Message.Contains(closedServer.Split(':')[0], StringComparison.Ordinal), listing.Message);
		context.Check("it isn't PrintSystemUnavailableException, since CUPS is configured", listing is not PrintSystemUnavailableException);

		PrintException? defaultError = await CatalogFailureScenarios.CatchAsync(() => fixture.Catalog.GetDefaultPrinterAsync(context.CancellationToken));
		context.Check("finding the default printer throws too", defaultError is not null, defaultError?.Message);
	}

	/// <summary>
	/// Runs <see cref="NoPrintSystem"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task NoPrintSystemAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = new CatalogFixture(services => { });

		PrintException? listing = await CatalogFailureScenarios.CatchAsync(() => fixture.Catalog.GetPrintersAsync(context.CancellationToken));
		PrintException? lookup = await CatalogFailureScenarios.CatchAsync(() => fixture.Catalog.GetPrinterAsync(TestPrinters.CupsRaw, context.CancellationToken));
		PrintException? defaultError = await CatalogFailureScenarios.CatchAsync(() => fixture.Catalog.GetDefaultPrinterAsync(context.CancellationToken));

		context.Check("listing throws PrintSystemUnavailableException", listing is PrintSystemUnavailableException, listing?.GetType().Name ?? "nothing thrown");
		context.Check("lookup throws PrintSystemUnavailableException", lookup is PrintSystemUnavailableException, lookup?.GetType().Name ?? "nothing thrown");
		context.Check("the default printer throws PrintSystemUnavailableException", defaultError is PrintSystemUnavailableException, defaultError?.GetType().Name ?? "nothing thrown");
		context.Note("Message: " + listing?.Message);
	}

	/// <summary>
	/// Runs <see cref="UnknownIds"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task UnknownIdsAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);

		await using CatalogFixture fixture = new CatalogFixture
		(
			services =>
			{
				services.AddLatiprimereIpp(options =>
				{
					options.EnableDiscovery = false;
					options.PrinterUris.Add(fake.PrinterUri);
				});

				if (OperatingSystem.IsWindows())
				{
					services.AddLatiprimerePdfium().AddLatiprimereWindows();
				}
			}
		);

		// Same printer, another path: a URI that isn't configured isn't looked up (ADR 0024)
		Uri unconfigured = new Uri(fake.PrinterUri, "/ipp/other");
		List<string> unknownIds = ["nosuch:printer", TestPrinters.GetIppPrinterId(unconfigured)];

		if (OperatingSystem.IsWindows())
		{
			unknownIds.Add("windows:Latiprimere No Such Printer");
		}

		foreach (string printerId in unknownIds)
		{
			PrintException? exception = await CatalogFailureScenarios.CatchAsync(() => fixture.Catalog.GetPrinterAsync(printerId, context.CancellationToken));
			PrinterNotFoundException? notFound = exception as PrinterNotFoundException;
			context.Check($"{printerId} is reported as not found", notFound is not null, exception is null ? "nothing thrown" : $"{exception.GetType().Name}: {exception.Message}");
			context.CheckEqual($"the exception names {printerId}", printerId, notFound?.PrinterId);
		}

		context.CheckEqual("the unconfigured URI was never asked", 0, fake.Requests.Count(request => request.Find("printer-uri")?.Value == unconfigured.AbsoluteUri));
	}

	/// <summary>
	/// Gets a loopback address where nothing listens: a fake printer's, once it has stopped.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <returns>The address as <c>host:port</c>.</returns>
	private static async Task<string> GetClosedAddressAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinter.StartAsync(cancellationToken: context.CancellationToken);

		return $"127.0.0.1:{fake.PrinterUri.Port}";
	}

	/// <summary>
	/// Runs an action that should fail, and returns what it threw.
	/// </summary>
	/// <param name="action">The action.</param>
	/// <returns>The <see cref="PrintException"/> it threw, or <see langword="null"/> if it succeeded.</returns>
	private static async Task<PrintException?> CatchAsync(Func<Task> action)
	{
		try
		{
			await action();

			return null;
		}
		catch (PrintException exception)
		{
			return exception;
		}
	}
}
