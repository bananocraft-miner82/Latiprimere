using System.Globalization;
using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// Looks up and prints to the printer a person named in manual mode, which may be real. Every job is shown to the
/// person first and prints only once they say yes.
/// </summary>
public static class ManualPrinting
{
	/// <summary>
	/// The longest a job on a real printer may take: warming up, two-sided pages and copies can take minutes.
	/// </summary>
	public static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(10);

	/// <summary>
	/// The longest a manual scenario may run, with a person reading pages and answering between jobs.
	/// </summary>
	public static readonly TimeSpan ScenarioTimeout = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Looks up the printer the person named, through every print system, as an application registers them: CUPS
	/// (with <c>CUPS_SERVER</c> if set), the Windows spooler, and direct IPP with mDNS discovery.
	/// </summary>
	/// <param name="context">The scenario; the catalog is kept until it ends.</param>
	/// <param name="configureWindows">Changes the Windows print system's options, or <see langword="null"/>.</param>
	/// <returns>The printer.</returns>
	/// <exception cref="ScenarioCheckFailedException">No printer was named.</exception>
	public static async Task<IPrinter> GetPrinterAsync(ScenarioContext context, Action<Latiprimere.Windows.WindowsOptions>? configureWindows = null)
	{
		ArgumentNullException.ThrowIfNull(context);

		string? printerId = context.Targets.ManualPrinterId;

		if (printerId is null)
		{
			context.Require("a printer was named for manual checks", false);
		}

		Uri? ippUri = ManualPrinting.FindIppUri(printerId!);

		CatalogFixture fixture = new CatalogFixture
		(
			services =>
			{
				services.AddLatiprimereCups();
				services.AddLatiprimereIpp(options =>
				{
					if (printerId!.StartsWith("ipp:", StringComparison.Ordinal) && ippUri is not null)
					{
						options.PrinterUris.Add(ippUri);
					}
				});

				if (OperatingSystem.IsWindows())
				{
					services.AddLatiprimerePdfium().AddLatiprimereWindows(configureWindows);
				}
			}
		);

		context.RegisterForDisposal(fixture);

		return await fixture.Catalog.GetPrinterAsync(printerId!, context.CancellationToken);
	}

	/// <summary>
	/// Finds the IPP address of a printer ID, for capturing its attributes: an <c>ipp:</c> ID with a URI, or a CUPS
	/// queue on <c>CUPS_SERVER</c> (or <c>localhost:631</c>).
	/// </summary>
	/// <param name="printerId">The printer ID.</param>
	/// <returns>The URI, or <see langword="null"/> for a Windows printer or one found by mDNS.</returns>
	public static Uri? FindIppUri(string printerId)
	{
		ArgumentNullException.ThrowIfNull(printerId);

		if (printerId.StartsWith("ipp:", StringComparison.Ordinal)
			&& Uri.TryCreate(printerId["ipp:".Length..], UriKind.Absolute, out Uri? uri)
			&& uri.Scheme is "ipp" or "ipps")
		{
			return uri;
		}

		if (printerId.StartsWith("cups:", StringComparison.Ordinal))
		{
			string server = Environment.GetEnvironmentVariable("CUPS_SERVER") is { Length: > 0 } configured && !configured.StartsWith('/')
				? configured
				: "localhost:631";

			return new Uri($"ipp://{server}/printers/{Uri.EscapeDataString(printerId["cups:".Length..])}");
		}

		return null;
	}

	/// <summary>
	/// Shows the person what is about to print and, once they say yes, prints it and follows it to the end. If they
	/// say no, the scenario ends as skipped.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="printer">The printer the person named.</param>
	/// <param name="documentName">The test document.</param>
	/// <param name="options">How to print it.</param>
	/// <returns>The job's final status.</returns>
	public static async Task<PrintJobStatus> PrintAsync(ScenarioContext context, IPrinter printer, string documentName, PrintOptions options)
	{
		(_, PrintJobStatus status) = await ManualPrinting.PrintJobAsync(context, printer, documentName, options);

		return status;
	}

	/// <summary>
	/// Prints a test document once the person agrees, as <see cref="PrintAsync"/> does, and gives the job too.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="printer">The printer.</param>
	/// <param name="documentName">The test document's file name.</param>
	/// <param name="options">How to print it.</param>
	/// <returns>The job, and how it ended.</returns>
	public static async Task<(IPrintJob Job, PrintJobStatus Status)> PrintJobAsync(ScenarioContext context, IPrinter printer, string documentName, PrintOptions options)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(printer);
		ArgumentNullException.ThrowIfNull(options);

		int pages = PdfPrinters.InspectSource(documentName).PageCount;
		bool isTwoSided = options.Duplex is DuplexMode.LongEdge or DuplexMode.ShortEdge;
		int sheets = (isTwoSided ? (pages + 1) / 2 : pages) * options.Copies;
		string size = $"{ManualPrinting.Count(pages, "page")}, {ManualPrinting.Count(options.Copies, "copy", "copies")}: {ManualPrinting.Count(sheets, "sheet")}";

		context.Person.Tell($"About to print {documentName} ({size}) on {printer.Info.Name} ({printer.Info.Id}).{Environment.NewLine}Options: {ManualPrinting.Describe(options)}.");

		if (!await context.Person.ConfirmAsync("Print it on paper now?", context.CancellationToken))
		{
			context.Skip("the person chose not to print it");
		}

		// Microsoft Print to PDF would otherwise ask where to save
		if (printer.Info.Id == TestPrinters.MicrosoftPrintToPdf)
		{
			options = options with { OutputFile = context.GetEvidencePath($"{documentName}.pdf") };
		}

		using FileStream document = TestDocuments.Open(documentName);
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options with { JobName = options.JobName ?? "Latiprimere manual check" }, context.CancellationToken);
		context.Note($"Submitted job {job.Id} ({job.GetType().Name}).");
		IReadOnlyList<PrintJobStatus> changes = await JobFollower.FollowAsync(context, job, ManualPrinting.JobTimeout);
		PrintJobStatus final = changes[^1];
		context.CheckEqual("the job completes", PrintJobState.Completed, final.State);

		return (job, final);
	}

	/// <summary>
	/// Counts something in words, such as <c>1 page</c> or <c>3 pages</c>.
	/// </summary>
	/// <param name="count">How many.</param>
	/// <param name="singular">The word for one.</param>
	/// <param name="plural">The word for more, or <see langword="null"/> to add an s.</param>
	/// <returns>The count and the word.</returns>
	private static string Count(int count, string singular, string? plural = null)
	{
		return string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? singular : plural ?? singular + "s")}");
	}

	/// <summary>
	/// Describes print options for the person.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <returns>Such as <c>A4, monochrome, landscape</c>, or <c>the printer's defaults</c>.</returns>
	public static string Describe(PrintOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		List<string> parts = new List<string>();

		if (options.MediaSize is not null)
		{
			parts.Add($"{options.MediaSize.DisplayName} paper");
		}

		if (options.InputTray is not null)
		{
			parts.Add($"paper from the {options.InputTray.Name} tray");
		}

		if (options.ColorMode != ColorMode.PrinterDefault)
		{
			parts.Add(options.ColorMode == ColorMode.Monochrome ? "monochrome" : "colour");
		}

		if (options.Duplex != DuplexMode.PrinterDefault)
		{
			parts.Add(options.Duplex switch
			{
				DuplexMode.LongEdge => "two-sided, turning on the long edge",
				DuplexMode.ShortEdge => "two-sided, turning on the short edge",
				_ => "one-sided"
			});
		}

		if (options.Orientation != PageOrientation.PrinterDefault)
		{
			parts.Add(options.Orientation == PageOrientation.Landscape ? "landscape" : "portrait");
		}

		if (options.Copies > 1)
		{
			parts.Add(string.Create(CultureInfo.InvariantCulture, $"{options.Copies} copies"));
		}

		return parts.Count == 0 ? "the printer's defaults" : string.Join(", ", parts);
	}
}
