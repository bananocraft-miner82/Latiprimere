using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Windows;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Vendor options listed in the capabilities, then sent back as a job's options (ADR 0032).
/// </summary>
public static class VendorOptionScenarios
{
	/// <summary>
	/// An IPP printer's job attributes are listed with their values, and a listed value reaches the printer.
	/// </summary>
	public static readonly Scenario IppListedAndSent = new Scenario
	{
		Id = "ipp.vendor-options.listed-and-sent",
		Area = ScenarioArea.Capabilities,
		Title = "The HP ENVY 5640's and ippeveprinter's captured attributes list print quality and resolution as vendor options, and a value taken from the list reaches the printer with the job",
		Needs = TargetNeeds.None,
		RunAsync = VendorOptionScenarios.IppListedAndSentAsync
	};

	/// <summary>
	/// Microsoft Print to PDF's driver features are listed, its Print Capabilities read, and a listed feature
	/// printed with.
	/// </summary>
	public static readonly Scenario WindowsListedAndSent = new Scenario
	{
		Id = "windows.vendor-options.listed-and-sent",
		Area = ScenarioArea.Capabilities,
		Title = "Microsoft Print to PDF's driver features are listed as vendor options, its Print Capabilities and default Print Ticket read, a listed feature printed with, and a feature it lacks refused before anything prints",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = VendorOptionScenarios.WindowsListedAndSentAsync
	};

	/// <summary>
	/// Runs <see cref="IppListedAndSent"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task IppListedAndSentAsync(ScenarioContext context)
	{
		// A real printer's attributes, listed
		FakePrinterProfile hp = await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken);
		IReadOnlyList<VendorOption> hpOptions = await VendorOptionScenarios.ReadVendorOptionsAsync(context, hp);
		await VendorOptionScenarios.SaveAsync(context, "hp-envy-5640-vendor-options.txt", hpOptions);

		VendorOption? quality = hpOptions.FirstOrDefault(option => option.Key == "ipp:print-quality");
		context.Require("the HP lists ipp:print-quality", quality is not null);
		context.CheckEqual("its choices are Draft, Normal and High", "3=Draft, 4=Normal, 5=High", string.Join(", ", quality!.Choices.Select(choice => $"{choice.Value}={choice.DisplayName}")));
		context.CheckEqual("its default is Normal", "4", quality.DefaultValue);

		VendorOption? resolution = hpOptions.FirstOrDefault(option => option.Key == "ipp:printer-resolution");
		context.Check("the HP lists 1200 dpi among its resolutions", resolution?.Choices.Any(choice => choice.Value == "1200dpi") == true, string.Join(", ", resolution?.Choices.Select(choice => choice.Value) ?? []));
		context.Check("settings the typed options cover aren't listed", !hpOptions.Any(option => option.Key is "ipp:copies" or "ipp:sides" or "ipp:media" or "ipp:print-color-mode"));

		// Then one that accepts PDF, given back a value from its own list
		FakePrinterProfile profile = await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken);
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);

		VendorOption? listed = capabilities.VendorOptions.FirstOrDefault(option => option.Key == "ipp:print-quality");
		VendorOptionChoice? high = listed?.Choices.FirstOrDefault(choice => choice.DisplayName == "High");
		context.Require("ippeveprinter lists High print quality", high is not null);

		byte[] pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", TestDocuments.MixedOnePage), context.CancellationToken);
		using MemoryStream document = new MemoryStream(pdf);
		PrintOptions options = new PrintOptions { VendorOptions = new Dictionary<string, string> { [listed!.Key] = high!.Value } };

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("print-quality arrives as the listed value", high.Value, printJob.Find("print-quality")?.Value);
	}

	/// <summary>
	/// Runs <see cref="WindowsListedAndSent"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WindowsListedAndSentAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);
		IWindowsPrinter? windowsPrinter = printer as IWindowsPrinter;
		context.Require("the printer is an IWindowsPrinter", windowsPrinter is not null);

		string printCapabilities = await windowsPrinter!.GetPrintCapabilitiesAsync(cancellationToken: context.CancellationToken);
		string defaultTicket = await windowsPrinter.GetDefaultPrintTicketAsync(context.CancellationToken);
		await context.SaveEvidenceAsync("print-capabilities.xml", [printCapabilities], "The driver's Print Capabilities");
		await context.SaveEvidenceAsync("default-print-ticket.xml", [defaultTicket], "The driver's default Print Ticket");
		context.Check("the Print Capabilities document lists paper sizes", printCapabilities.Contains("psk:PageMediaSize", StringComparison.Ordinal));
		context.Check("the default Print Ticket is a Print Ticket", defaultTicket.Contains("PrintTicket", StringComparison.Ordinal));

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		await VendorOptionScenarios.SaveAsync(context, "vendor-options.txt", capabilities.VendorOptions);
		VendorOption? resolution = capabilities.VendorOptions.FirstOrDefault(option => option.Key == "windows:feature:psk:PageResolution");
		context.Require("the driver's resolution feature is listed", resolution is { Choices.Count: > 0 });
		context.Check("paper size isn't listed, since MediaSize covers it", !capabilities.VendorOptions.Any(option => option.Key == "windows:feature:psk:PageMediaSize"));

		VendorOptionChoice choice = resolution!.Choices[0];
		PrintOptions options = new PrintOptions { VendorOptions = new Dictionary<string, string> { [resolution.Key] = choice.Value } };
		PdfSummary printed = await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.MixedOnePage, options, "listed-feature");
		context.CheckEqual("with the listed feature, one page is printed", 1, printed.Pages.Count);

		// A feature the driver doesn't have is refused before rendering
		PrintOptions unknown = new PrintOptions { VendorOptions = new Dictionary<string, string> { ["windows:feature:psk:JobStapleAllDocuments"] = "psk:StapleTopLeft" } };
		using MemoryStream document = new MemoryStream(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", TestDocuments.MixedOnePage), context.CancellationToken));
		Exception? refused = null;

		try
		{
			await printer.SubmitAsync(PrintContent.FromPdf(document), unknown, context.CancellationToken);
		}
		catch (ArgumentException exception)
		{
			refused = exception;
		}

		context.Check("stapling, which it lacks, is refused with ArgumentException", refused is not null, refused?.Message ?? "not refused");
	}

	/// <summary>
	/// Serves a profile from a fake printer and reads its vendor options.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <param name="profile">The profile.</param>
	/// <returns>The vendor options.</returns>
	private static async Task<IReadOnlyList<VendorOption>> ReadVendorOptionsAsync(ScenarioContext context, FakePrinterProfile profile)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		return (await printer.GetCapabilitiesAsync(context.CancellationToken)).VendorOptions;
	}

	/// <summary>
	/// Saves vendor options as text, one per line.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <param name="fileName">The evidence file's name.</param>
	/// <param name="options">The options.</param>
	/// <returns>A task that completes when the file is written.</returns>
	internal static Task SaveAsync(ScenarioContext context, string fileName, IReadOnlyList<VendorOption> options)
	{
		return context.SaveEvidenceAsync(fileName, VendorOptionScenarios.Describe(options), "The vendor options Latiprimere listed");
	}

	/// <summary>
	/// Describes vendor options, one per line: the key, the name, and the values with the default marked.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <returns>The lines.</returns>
	internal static List<string> Describe(IReadOnlyList<VendorOption> options)
	{
		return options
			.Select(option => option.Kind switch
			{
				VendorOptionKind.Choice => $"{option.Key} ({option.DisplayName}): " + string.Join(", ", option.Choices.Select(choice => $"{choice.Value} = {choice.DisplayName}{(choice.Value == option.DefaultValue ? " (default)" : string.Empty)}")),
				VendorOptionKind.Number => $"{option.Key} ({option.DisplayName}): a number from {option.MinimumValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "…"} to {option.MaximumValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "…"}, default {option.DefaultValue ?? "not reported"}",
				_ => $"{option.Key} ({option.DisplayName}): text, default {option.DefaultValue ?? "not reported"}",
			})
			.ToList();
	}
}
