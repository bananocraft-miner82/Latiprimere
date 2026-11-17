using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// What printers report they can do, from attributes captured from real printers and served by the scripted printer.
/// </summary>
public static class IppCapabilitiesScenarios
{
	/// <summary>
	/// The HP ENVY 5640's own attributes: two-sided and colour, and a PDF refused before anything is sent, since it
	/// doesn't take PDF.
	/// </summary>
	public static readonly Scenario HpEnvyProfile = new Scenario
	{
		Id = "ipp.capabilities.hp-envy-5640",
		Area = ScenarioArea.Capabilities,
		Title = "Served the HP ENVY 5640's captured attributes, Latiprimere reports two-sided and colour, and refuses a PDF before sending it, since that printer doesn't take PDF",
		Needs = TargetNeeds.None,
		RunAsync = IppCapabilitiesScenarios.HpEnvyProfileAsync
	};

	/// <summary>
	/// Runs <see cref="HpEnvyProfile"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task HpEnvyProfileAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken);
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		context.Note($"Make and model: {printer.Info.MakeAndModel}");

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		await context.SaveEvidenceAsync
		(
			"capabilities.txt",
			[
				"Paper sizes: " + string.Join(", ", capabilities.MediaSizes.Select(size => size.Name)),
				"Duplex: " + string.Join(", ", capabilities.DuplexModes),
				"Colour: " + string.Join(", ", capabilities.ColorModes),
				"Trays: " + string.Join(", ", capabilities.InputTrays.Select(tray => tray.Id)),
				$"Copies: up to {capabilities.MaxCopies}"
			],
			"The capabilities Latiprimere read"
		);

		context.Check("it reports long-edge two-sided printing", capabilities.DuplexModes.Contains(DuplexMode.LongEdge), string.Join(", ", capabilities.DuplexModes));
		context.Check("it reports colour", capabilities.ColorModes.Contains(ColorMode.Color), string.Join(", ", capabilities.ColorModes));
		context.Check("it reports A4", capabilities.MediaSizes.Any(size => size.Name == "iso_a4_210x297mm"));
		context.Check("the printer doesn't list PDF", !profile.GetValues("document-format-supported").Contains("application/pdf"));

		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		NotSupportedException? exception = null;

		try
		{
			await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		}
		catch (NotSupportedException caught)
		{
			exception = caught;
		}

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Check("a PDF is refused with NotSupportedException", exception is not null, exception?.Message);
		context.CheckEqual("nothing was sent to the printer", 0, fake.GetRequests(IppOperation.PrintJob).Count);
	}
}
