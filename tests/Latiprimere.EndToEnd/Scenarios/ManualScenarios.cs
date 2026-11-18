using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Checks a person makes on a printer they name, which may be real: the console harness's manual mode (ADR 0029,
/// decision 5). Nothing here runs unattended: each scenario needs a person, and only manual mode provides one.
/// </summary>
/// <remarks>
/// <para>
/// The first scenario only reads the printer. Each of the others prints one job, after showing the person the
/// document, page count and options and waiting for a yes, then asks what came out.
/// </para>
/// <para>
/// A scenario whose option the printer doesn't report is skipped, rather than asking for something it can't do.
/// </para>
/// </remarks>
public static class ManualScenarios
{
	/// <summary>
	/// The printer's details and capabilities are right, and its IPP attributes are captured.
	/// </summary>
	public static readonly Scenario PrinterDetails = new Scenario
	{
		Id = "manual.printer-details",
		Area = ScenarioArea.Manual,
		Title = "The printer's name, model and capabilities are what the person knows it has, and its IPP attributes are saved (reads only)",
		Needs = TargetNeeds.Person,
		Timeout = ManualPrinting.ScenarioTimeout,
		RunAsync = ManualScenarios.PrinterDetailsAsync
	};

	/// <summary>
	/// A colour test page prints whole.
	/// </summary>
	public static readonly Scenario TestPage = new Scenario
	{
		Id = "manual.test-page",
		Area = ScenarioArea.Manual,
		Title = "A one-page PDF prints with the printer's defaults: one page, all of it there, nothing cut off",
		Needs = TargetNeeds.Person,
		Timeout = ManualPrinting.ScenarioTimeout,
		RunAsync = ManualScenarios.TestPageAsync
	};

	/// <summary>
	/// The chosen paper and monochrome are carried out.
	/// </summary>
	public static readonly Scenario PaperAndColour = new Scenario
	{
		Id = "manual.paper-and-colour",
		Area = ScenarioArea.Manual,
		Title = "The page prints on A4 (or Letter) chosen from the capabilities, and in grey only when monochrome is chosen",
		Needs = TargetNeeds.Person,
		Timeout = ManualPrinting.ScenarioTimeout,
		RunAsync = ManualScenarios.PaperAndColourAsync
	};

	/// <summary>
	/// Two copies of a three-page document print in order, two-sided where the printer can.
	/// </summary>
	public static readonly Scenario TwoSidedAndCopies = new Scenario
	{
		Id = "manual.two-sided-and-copies",
		Area = ScenarioArea.Manual,
		Title = "Two copies of a three-page PDF print collated, in page order, and two-sided when the printer reports it can",
		Needs = TargetNeeds.Person,
		Timeout = ManualPrinting.ScenarioTimeout,
		RunAsync = ManualScenarios.TwoSidedAndCopiesAsync
	};

	/// <summary>
	/// Landscape turns the page.
	/// </summary>
	public static readonly Scenario Landscape = new Scenario
	{
		Id = "manual.landscape",
		Area = ScenarioArea.Manual,
		Title = "With landscape chosen, the page prints across the paper",
		Needs = TargetNeeds.Person,
		Timeout = ManualPrinting.ScenarioTimeout,
		RunAsync = ManualScenarios.LandscapeAsync
	};

	/// <summary>
	/// The chosen tray feeds the paper.
	/// </summary>
	public static readonly Scenario Tray = new Scenario
	{
		Id = "manual.tray",
		Area = ScenarioArea.Manual,
		Title = "The paper comes from the tray the person chooses from the capabilities",
		Needs = TargetNeeds.Person,
		Timeout = ManualPrinting.ScenarioTimeout,
		RunAsync = ManualScenarios.TrayAsync
	};

	/// <summary>
	/// A Windows WSD queue's PDF goes straight to its printer over IPP, found by the UUID the queue records, as PDF or
	/// as raster, and the job reports how it ended (ADR 0036).
	/// </summary>
	public static readonly Scenario WindowsQueueOverIpp = new Scenario
	{
		Id = "manual.windows-queue-over-ipp",
		Area = ScenarioArea.Manual,
		Title = "With PrintPdfOverIpp on, a three-page PDF to a Windows WSD queue goes over IPP to the printer with the queue's UUID, as PDF or raster, two-sided when it can, and completes",
		Needs = TargetNeeds.Person,
		Timeout = ManualPrinting.ScenarioTimeout,
		RunAsync = ManualScenarios.WindowsQueueOverIppAsync
	};

	/// <summary>
	/// Runs <see cref="PrinterDetails"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PrinterDetailsAsync(ScenarioContext context)
	{
		IPrinter printer = await ManualPrinting.GetPrinterAsync(context);
		PrinterInfo info = printer.Info;
		context.Person.Tell($"{info.Name} ({info.Id}): {info.MakeAndModel ?? "model not reported"}, {info.State}, at {info.Location ?? "location not reported"}.");
		await context.AskPersonAsync("the printer's name and model are right", "Is that the printer you mean, with the right make and model?");

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		List<string> lines =
		[
			"Paper sizes: " + string.Join(", ", capabilities.MediaSizes.Select(size => size.DisplayName)),
			$"Default paper: {capabilities.DefaultMediaSize?.DisplayName ?? "not reported"}",
			"Sides: " + string.Join(", ", capabilities.DuplexModes),
			"Colour: " + string.Join(", ", capabilities.ColorModes),
			"Trays: " + string.Join(", ", capabilities.InputTrays.Select(tray => tray.Name)),
			$"Most copies: {capabilities.MaxCopies?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "not reported"}"
		];

		await context.SaveEvidenceAsync("capabilities.txt", lines, "The capabilities Latiprimere read");
		context.Person.Tell(string.Join(Environment.NewLine, lines));
		await context.AskPersonAsync("the capabilities match the printer", "Do the paper sizes, sides, colour and trays match what the printer can do?");

		// Everything else the printer offers, and on Windows the driver's own description of it (ADR 0032)
		await VendorOptionScenarios.SaveAsync(context, "vendor-options.txt", capabilities.VendorOptions);
		context.Note($"{capabilities.VendorOptions.Count} vendor options listed.");

		if (printer is Latiprimere.Windows.IWindowsPrinter windowsPrinter)
		{
			try
			{
				string printCapabilities = await windowsPrinter.GetPrintCapabilitiesAsync(cancellationToken: context.CancellationToken);
				string defaultTicket = await windowsPrinter.GetDefaultPrintTicketAsync(context.CancellationToken);
				await context.SaveEvidenceAsync("print-capabilities.xml", [printCapabilities], "The driver's Print Capabilities");
				await context.SaveEvidenceAsync("default-print-ticket.xml", [defaultTicket], "The driver's default Print Ticket");
			}
			catch (PrintException exception)
			{
				context.Note($"The driver's Print Capabilities couldn't be read: {exception.Message}");
			}
		}

		Uri? ippUri = ManualPrinting.FindIppUri(info.Id);

		if (ippUri is null)
		{
			context.Note("Not an IPP printer Latiprimere has an address for, so its attributes aren't captured.");

			return;
		}

		IppCaptureResult capture = await IppCapture.CaptureAsync(ippUri, context.EvidenceDirectory, "printer-attributes", context.CancellationToken);
		context.AddEvidence(capture.IppPath, "The printer's Get-Printer-Attributes response");
		context.AddEvidence(capture.TextPath, "The same attributes as text");
		context.Check("the printer's IPP attributes are saved", capture.AttributeCount > 0, $"{capture.AttributeCount} attributes");
		context.Note($"Replace these before sharing the capture: {string.Join("; ", capture.IdentifyingValues)}");
	}

	/// <summary>
	/// Runs <see cref="TestPage"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task TestPageAsync(ScenarioContext context)
	{
		IPrinter printer = await ManualPrinting.GetPrinterAsync(context);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);

		await ManualPrinting.PrintAsync(context, printer, TestDocuments.MixedOnePage, PrintOptions.Default);
		await context.AskPersonAsync("one page came out", "Did exactly one page come out?");
		await context.AskPersonAsync("the whole page is there", "Are the heading, the text, the shapes, the rule and the picture all there, with nothing cut off at the edges?");

		if (capabilities.DefaultColorMode == ColorMode.Color)
		{
			await context.AskPersonAsync("the picture is in colour", "Is the picture in colour?");
		}
	}

	/// <summary>
	/// Runs <see cref="PaperAndColour"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PaperAndColourAsync(ScenarioContext context)
	{
		IPrinter printer = await ManualPrinting.GetPrinterAsync(context);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		MediaSize? paper = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a4_210x297mm")
			?? capabilities.MediaSizes.FirstOrDefault(size => size.Name == "na_letter_8.5x11in");
		bool hasMonochrome = capabilities.ColorModes.Contains(ColorMode.Monochrome);

		if (paper is null && !hasMonochrome)
		{
			context.Skip("the printer reports neither A4 nor Letter, nor monochrome");
		}

		PrintOptions options = new PrintOptions
		{
			MediaSize = paper,
			ColorMode = hasMonochrome ? ColorMode.Monochrome : ColorMode.PrinterDefault
		};

		await ManualPrinting.PrintAsync(context, printer, TestDocuments.MixedOnePage, options);

		if (paper is not null)
		{
			await context.AskPersonAsync($"the page is on {paper.DisplayName}", $"Is it printed on {paper.DisplayName} paper ({paper.WidthMillimetres:0} x {paper.HeightMillimetres:0} mm)?");
		}

		if (hasMonochrome)
		{
			await context.AskPersonAsync("the page is in grey only", "Is the page in black and grey only, with no colour, the picture included?");
		}
	}

	/// <summary>
	/// Runs <see cref="TwoSidedAndCopies"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task TwoSidedAndCopiesAsync(ScenarioContext context)
	{
		IPrinter printer = await ManualPrinting.GetPrinterAsync(context);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		bool hasTwoSided = capabilities.DuplexModes.Contains(DuplexMode.LongEdge);
		int copies = Math.Min(2, capabilities.MaxCopies ?? 2);

		PrintOptions options = new PrintOptions
		{
			Copies = copies,
			Duplex = hasTwoSided ? DuplexMode.LongEdge : DuplexMode.PrinterDefault
		};

		await ManualPrinting.PrintAsync(context, printer, TestDocuments.ThreePages, options);

		string copiesText = copies == 1 ? "one copy" : $"{copies} copies";
		await context.AskPersonAsync($"{copiesText} came out, each with pages 1 to 3 in order", $"Did {copiesText} come out, each with pages 1, 2 and 3 in that order (the lines are numbered)?");

		if (hasTwoSided)
		{
			await context.AskPersonAsync("each copy is two-sided, turning like a book", "Is each copy on two sheets, printed on both sides of the first, turning like a book?");
		}
		else
		{
			context.Note("The printer doesn't report two-sided printing, so it wasn't asked for.");
		}
	}

	/// <summary>
	/// Runs <see cref="WindowsQueueOverIpp"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WindowsQueueOverIppAsync(ScenarioContext context)
	{
		IPrinter printer = await ManualPrinting.GetPrinterAsync(context, options => options.PrintPdfOverIpp = true);

		if (printer is not Latiprimere.Windows.IWindowsPrinter)
		{
			context.Skip("the printer named isn't a Windows queue: name one such as windows:HP ENVY 5640 series");
		}

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		bool hasTwoSided = capabilities.DuplexModes.Contains(DuplexMode.LongEdge);

		PrintOptions options = new PrintOptions
		{
			JobName   = "Latiprimere Windows queue over IPP",
			MediaSize = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a4_210x297mm"),
			Duplex    = hasTwoSided ? DuplexMode.LongEdge : DuplexMode.PrinterDefault
		};

		context.Person.Tell("If the queue's printer announces itself over mDNS with the UUID its WSD port records, the job goes to it over IPP, not through the driver.");
		(IPrintJob job, _) = await ManualPrinting.PrintJobAsync(context, printer, TestDocuments.ThreePages, options);

		// A routed job is the printer's IPP job, followed to how it ended; a spooled one is the queue's
		context.CheckEqual("the job went over IPP, not through the driver", "IppPrintJob", job.GetType().Name);
		await context.AskPersonAsync("pages 1 to 3 came out in order", "Did pages 1, 2 and 3 come out, in that order (the lines are numbered)?");

		if (hasTwoSided)
		{
			await context.AskPersonAsync("they're two-sided, turning like a book", "Are they on two sheets, printed on both sides of the first, turning like a book?");
		}
	}

	/// <summary>
	/// Runs <see cref="Landscape"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task LandscapeAsync(ScenarioContext context)
	{
		IPrinter printer = await ManualPrinting.GetPrinterAsync(context);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);

		if (!capabilities.Orientations.Contains(PageOrientation.Landscape))
		{
			context.Skip("the printer doesn't report landscape");
		}

		await ManualPrinting.PrintAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { Orientation = PageOrientation.Landscape });

		// CUPS keeps a PDF's own orientation, so this is expected to fail on CUPS queues
		await context.AskPersonAsync("the page is landscape", "Is the page printed across the paper, so it reads with the paper turned sideways?");
	}

	/// <summary>
	/// Runs <see cref="Tray"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task TrayAsync(ScenarioContext context)
	{
		IPrinter printer = await ManualPrinting.GetPrinterAsync(context);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);

		if (capabilities.InputTrays.Count == 0)
		{
			context.Skip("the printer reports no trays");
		}

		int? choice = await context.Person.ChooseAsync
		(
			"Which tray should the page come from? It should have paper in it.",
			capabilities.InputTrays.Select(tray => $"{tray.Name} ({tray.Id})").ToList(),
			context.CancellationToken
		);

		if (choice is null)
		{
			context.Skip("the person chose no tray");
		}

		InputTray tray = capabilities.InputTrays[choice.Value];
		await ManualPrinting.PrintAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { InputTray = tray });
		await context.AskPersonAsync($"the paper came from {tray.Name}", $"Did the paper come from {tray.Name}?");
	}
}
