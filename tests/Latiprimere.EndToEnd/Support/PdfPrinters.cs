using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// Prints to the virtual printers that write PDFs, CUPS-PDF and Microsoft Print to PDF, and reads back what they
/// wrote, keeping it as evidence.
/// </summary>
public static class PdfPrinters
{
	/// <summary>
	/// How long before submitting a CUPS-PDF file may appear to have been written, allowing for the container's clock.
	/// </summary>
	private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Reads a test document as it was sent, for comparing with what a printer wrote.
	/// </summary>
	/// <param name="documentName">The test document, such as <see cref="TestDocuments.MixedOnePage"/>.</param>
	/// <returns>What it holds.</returns>
	public static PdfSummary InspectSource(string documentName)
	{
		return PdfInspector.Inspect(Path.Combine(AppContext.BaseDirectory, "Data", documentName));
	}

	/// <summary>
	/// Prints a test document to CUPS-PDF and reads the PDF it writes. The job must complete and the file appear,
	/// or the scenario ends as failed.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="printer">The CUPS-PDF queue.</param>
	/// <param name="documentName">The test document.</param>
	/// <param name="options">How to print it.</param>
	/// <param name="label">Names the evidence file, such as <c>monochrome</c>.</param>
	/// <returns>What CUPS-PDF wrote.</returns>
	public static async Task<PdfSummary> PrintToCupsPdfAsync(ScenarioContext context, IPrinter printer, string documentName, PrintOptions options, string label)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(printer);
		ArgumentNullException.ThrowIfNull(options);

		string pdfDirectory = Path.Combine(context.Targets.CupsOutputDirectory!, "pdf"); // Callers need CupsOutput
		using FileStream document = TestDocuments.Open(documentName);
		DateTime submittedAt = DateTime.UtcNow;

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options with { JobName = options.JobName ?? "Latiprimere end-to-end" }, context.CancellationToken);
		context.Note($"Submitted job {job.Id} ({label}).");
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		context.RequireEqual($"the {label} job completes", PrintJobState.Completed, status.State);

		string? outputFile = await PdfPrinters.WaitForCupsPdfFileAsync(pdfDirectory, job.Id, submittedAt - PdfPrinters.ClockTolerance, context.CancellationToken);
		context.Require($"CUPS-PDF writes a file for the {label} job", outputFile is not null, outputFile ?? $"no *-job_{job.Id}.pdf in {pdfDirectory}");
		byte[]? written = await OutputFileWaiter.WaitForAnyContentAsync(outputFile!, context.CancellationToken);
		context.Require($"the {label} file can be read", written is not null && written.Length > 0, outputFile);
		context.Require($"the {label} file is a PDF", PdfInspector.HasPdfHeader(written!));
		await context.SaveEvidenceAsync($"cups-pdf-{label}.pdf", written!, $"What CUPS-PDF wrote ({label})");

		return PdfPrinters.Describe(context, PdfInspector.Inspect(written!), label);
	}

	/// <summary>
	/// Prints a test document to Microsoft Print to PDF, into the evidence folder, and reads the PDF it writes. The
	/// job must complete and the file appear, or the scenario ends as failed.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="printer">Microsoft Print to PDF.</param>
	/// <param name="documentName">The test document.</param>
	/// <param name="options">How to print it; the output file is set here.</param>
	/// <param name="label">Names the output file, such as <c>landscape</c>.</param>
	/// <returns>What the driver wrote.</returns>
	public static async Task<PdfSummary> PrintToMicrosoftPdfAsync(ScenarioContext context, IPrinter printer, string documentName, PrintOptions options, string label)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(printer);
		ArgumentNullException.ThrowIfNull(options);

		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("Microsoft Print to PDF is a Windows printer.");
		}

		string outputFile = context.GetEvidencePath($"print-to-pdf-{label}.pdf");
		using FileStream document = TestDocuments.Open(documentName);
		PrintOptions withFile = options with { OutputFile = outputFile, JobName = options.JobName ?? "Latiprimere end-to-end" };
		byte[]? written;

		// Only one job at a time, across every test process, or the driver can stall and leave an empty file
		using (SharedPrinterLock printerLock = new SharedPrinterLock(TestPrinters.MicrosoftPrintToPdfName))
		{
			IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), withFile, context.CancellationToken);
			context.Note($"Submitted job {job.Id} ({label}).");
			PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
			context.RequireEqual($"the {label} job completes", PrintJobState.Completed, status.State);
			written = await OutputFileWaiter.WaitForAnyContentAsync(outputFile, context.CancellationToken);
		}

		context.Require($"the driver writes the {label} file", written is not null && written.Length > 0, outputFile);
		context.Require($"the {label} file is a PDF", PdfInspector.HasPdfHeader(written!));
		context.AddEvidence(outputFile, $"What Microsoft Print to PDF wrote ({label})");

		return PdfPrinters.Describe(context, PdfInspector.Inspect(written!), label);
	}

	/// <summary>
	/// Notes each page of a printed PDF.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="summary">What the printer wrote.</param>
	/// <param name="label">Which job it was.</param>
	/// <returns><paramref name="summary"/>.</returns>
	private static PdfSummary Describe(ScenarioContext context, PdfSummary summary, string label)
	{
		foreach (string line in summary.Describe())
		{
			context.Note($"{label}: {line}");
		}

		return summary;
	}

	/// <summary>
	/// Waits for CUPS-PDF's file for a job, which it names <c>&lt;title&gt;-job_&lt;id&gt;.pdf</c>.
	/// </summary>
	/// <param name="pdfDirectory">CUPS-PDF's output folder.</param>
	/// <param name="jobId">The CUPS job ID.</param>
	/// <param name="notBefore">Files written earlier are left from an earlier container with the same job IDs.</param>
	/// <param name="cancellationToken">Cancels the wait.</param>
	/// <returns>The file's path, or <see langword="null"/> if none appeared in time.</returns>
	private static async Task<string?> WaitForCupsPdfFileAsync(string pdfDirectory, string jobId, DateTime notBefore, CancellationToken cancellationToken)
	{
		DateTime deadline = DateTime.UtcNow + OutputFileWaiter.Timeout;
		string pattern = $"*-job_{jobId}.pdf";

		while (true)
		{
			string? match = Directory.Exists(pdfDirectory)
				? Directory.GetFiles(pdfDirectory, pattern).FirstOrDefault(path => File.GetLastWriteTimeUtc(path) >= notBefore)
				: null;

			if (match is not null || DateTime.UtcNow >= deadline)
			{
				return match;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
		}
	}
}
