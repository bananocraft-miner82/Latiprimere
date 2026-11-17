using Latiprimere.EndToEnd.Support;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Printing with options to the real <c>ippeveprinter</c>, through a CUPS IPP Everywhere queue and directly. These
/// are among the scenarios CI's Native AOT check runs (<c>tools/aot-check/check-aot.sh</c>), taking over what it once
/// checked with the CLI sample.
/// </summary>
public static class VirtualIppScenarios
{
	/// <summary>
	/// A PDF with monochrome and <c>ipp:</c> and <c>cups:</c> vendor options prints through the CUPS Virtual-IPP queue.
	/// </summary>
	public static readonly Scenario CupsVendorOptions = new Scenario
	{
		Id = "cups.submission.vendor-options",
		Area = ScenarioArea.Submission,
		Title = "A PDF with monochrome, ipp:print-quality and cups:fit-to-page completes on the CUPS Virtual-IPP queue in front of ippeveprinter",
		Needs = TargetNeeds.CupsServer,
		RunAsync = VirtualIppScenarios.CupsVendorOptionsAsync
	};

	/// <summary>
	/// A PDF with paper, tray, colour and vendor options prints straight to <c>ippeveprinter</c>, and a value it doesn't
	/// support is refused.
	/// </summary>
	public static readonly Scenario DirectOptions = new Scenario
	{
		Id = "ipp.submission.virtual-printer-options",
		Area = ScenarioArea.Submission,
		Title = "A PDF with A4, the manual tray, monochrome, ipp:print-quality and ipp:ipp-attribute-fidelity completes on ippeveprinter directly, which refuses print-quality 99",
		Needs = TargetNeeds.IppPrinter,
		RunAsync = VirtualIppScenarios.DirectOptionsAsync
	};

	/// <summary>
	/// Runs <see cref="CupsVendorOptions"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CupsVendorOptionsAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.CupsVirtualIpp, context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		PrintOptions options = new PrintOptions
		{
			ColorMode = ColorMode.Monochrome,
			VendorOptions = new Dictionary<string, string> { ["ipp:print-quality"] = "4", ["cups:fit-to-page"] = "true" }
		};

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
		context.Note($"Submitted job {job.Id}.");
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);
	}

	/// <summary>
	/// Runs <see cref="DirectOptions"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task DirectOptionsAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.GetIppPrinterId(context.Targets.IppPrinterUri!), context.CancellationToken); // Needed, so set

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		MediaSize? a4 = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a4_210x297mm");
		InputTray? manual = capabilities.InputTrays.FirstOrDefault(tray => tray.Id == "manual");
		context.Require("its capabilities include A4", a4 is not null, $"{capabilities.MediaSizes.Count} paper sizes");

		// ippeveprinter lists a manual tray; a build without one is still checked without it
		if (manual is null)
		{
			context.Note("ippeveprinter lists no manual tray, so none is chosen.");
		}

		PrintOptions options = new PrintOptions
		{
			MediaSize = a4,
			InputTray = manual,
			ColorMode = ColorMode.Monochrome,
			VendorOptions = new Dictionary<string, string> { ["ipp:print-quality"] = "5", ["ipp:ipp-attribute-fidelity"] = "true" }
		};

		using (FileStream document = TestDocuments.Open(TestDocuments.ThreePages))
		{
			IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
			context.Note($"Submitted job {job.Id}.");
			PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
			context.CheckEqual("the job with every option completes", PrintJobState.Completed, status.State);
		}

		// ippeveprinter supports print-quality 3 to 5, so refusing 99 shows the attribute reaches it
		PrintException? refusal = null;

		using (FileStream document = TestDocuments.Open(TestDocuments.ThreePages))
		{
			try
			{
				PrintOptions unsupported = new PrintOptions { VendorOptions = new Dictionary<string, string> { ["ipp:print-quality"] = "99" } };
				await printer.SubmitAsync(PrintContent.FromPdf(document), unsupported, context.CancellationToken);
			}
			catch (PrintException exception)
			{
				refusal = exception;
			}
		}

		context.Check("print-quality 99 is refused", refusal is not null, refusal?.Message ?? "accepted");
		context.CheckEqual("with the printer's status 0x040B", (int?)0x040B, refusal?.NativeErrorCode);
	}
}
