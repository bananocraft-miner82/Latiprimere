using System.Net;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// When the Windows print system's IPP route gives a PDF back to the spooler, and when it doesn't (ADR 0027). A
/// Windows test printer is mapped to the scripted printer, which records any job it gets.
/// </summary>
/// <remarks>
/// <para>
/// The test printers use the Generic / Text Only driver, which can't print the page images a PDF becomes: a PDF sent
/// to one stopped its queue in an error (found 2026-11-16). Latiprimere now refuses PDFs to that driver in the
/// spooler's path only, so a job that falls back to the spooler is seen by that refusal, naming the driver, while a
/// job the IPP printer takes is not refused at all.
/// </para>
/// <para>
/// Pages are rendered at 72 DPI, so a job that wrongly reached the driver would spool little: at the driver's own
/// resolution, three pages made a 380 MB spool file.
/// </para>
/// </remarks>
public static class WindowsIppFallbackScenarios
{
	/// <summary>
	/// A printer that doesn't take PDF leaves the job to the spooler.
	/// </summary>
	public static readonly Scenario NoPdf = new Scenario
	{
		Id = "windows.ipp-route.fallback-no-pdf",
		Area = ScenarioArea.Submission,
		Title = "A PDF to a queue whose IPP printer takes neither PDF nor a raster format (the HP ENVY's attributes without Apple raster) goes to the spooler instead (which refuses it for its text-only driver), and the printer gets no job",
		Needs = TargetNeeds.WindowsTestPrinters,
		RunAsync = WindowsIppFallbackScenarios.NoPdfAsync
	};

	/// <summary>
	/// A printer that has gone away leaves the next job to the spooler.
	/// </summary>
	public static readonly Scenario Unreachable = new Scenario
	{
		Id = "windows.ipp-route.fallback-unreachable",
		Area = ScenarioArea.Submission,
		Title = "Once its IPP printer is switched off, a queue's next PDF goes to the spooler instead (which refuses it for its text-only driver)",
		Needs = TargetNeeds.WindowsTestPrinters,
		RunAsync = WindowsIppFallbackScenarios.UnreachableAsync
	};

	/// <summary>
	/// A printer that wants a password leaves the job to the spooler.
	/// </summary>
	public static readonly Scenario PasswordRequired = new Scenario
	{
		Id = "windows.ipp-route.fallback-password",
		Area = ScenarioArea.Submission,
		Title = "A PDF to a queue whose IPP printer asks for a password goes to the spooler instead (which refuses it for its text-only driver)",
		Needs = TargetNeeds.WindowsTestPrinters,
		RunAsync = WindowsIppFallbackScenarios.PasswordRequiredAsync
	};

	/// <summary>
	/// A printer that says, in <c>uri-authentication-supported</c>, that it wants a password for jobs leaves them to the
	/// spooler without being sent one (ADR 0042).
	/// </summary>
	public static readonly Scenario PasswordForJobs = new Scenario
	{
		Id = "windows.ipp-route.fallback-password-for-jobs",
		Area = ScenarioArea.Submission,
		Title = "A PDF to a queue whose IPP printer answers questions freely but says it asks for a password to print goes to the spooler without Validate-Job or Print-Job being sent (which refuses it for its text-only driver)",
		Needs = TargetNeeds.WindowsTestPrinters,
		RunAsync = WindowsIppFallbackScenarios.PasswordForJobsAsync
	};

	/// <summary>
	/// A printer that refuses the job is reported, and the spooler doesn't print it as well.
	/// </summary>
	public static readonly Scenario RefusalThrown = new Scenario
	{
		Id = "windows.ipp-route.refusal-not-resent",
		Area = ScenarioArea.Submission,
		Title = "A job the IPP printer refuses is reported with the printer's status, by Validate-Job before the document is sent, and isn't printed through the spooler as well",
		Needs = TargetNeeds.WindowsTestPrinters,
		RunAsync = WindowsIppFallbackScenarios.RefusalThrownAsync
	};

	/// <summary>
	/// The paused Windows test printer, whose queue holds jobs until they are cancelled.
	/// </summary>
	private const string PausedPrinterName = "Latiprimere Paused";

	/// <summary>
	/// The Windows test printer that writes each job to a file.
	/// </summary>
	private const string RawPrinterName = "Latiprimere Raw";

	/// <summary>
	/// The file its port writes.
	/// </summary>
	private const string RawOutputFileName = "Latiprimere-Raw.prn";

	/// <summary>
	/// The render resolution, which keeps each spool file small.
	/// </summary>
	private const int RenderDpi = 72;

	/// <summary>
	/// Runs <see cref="NoPdf"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task NoPdfAsync(ScenarioContext context)
	{
		// Without image/urf: a printer that takes Apple raster gets the PDF as raster (ADR 0036)
		FakePrinterProfile profile = (await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken))
			.Set("document-format-supported", Tag.MimeMediaType, "application/vnd.hp-PCL", "image/jpeg", "application/PCLm", "application/octet-stream");

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = WindowsIppFallbackScenarios.CreateCatalog(WindowsIppFallbackScenarios.PausedPrinterName, fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync("windows:" + WindowsIppFallbackScenarios.PausedPrinterName, context.CancellationToken);

		await WindowsIppFallbackScenarios.SubmitToSpoolerAsync(context, printer);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.Check("the printer was asked which formats it takes", fake.GetRequests(IppOperation.GetPrinterAttributes).Count > 0);
		context.CheckEqual("the printer got no job", 0, fake.GetRequests(IppOperation.PrintJob).Count);
	}

	/// <summary>
	/// Runs <see cref="Unreachable"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task UnreachableAsync(ScenarioContext context)
	{
		FakePrinter fake = await FakePrinters.StartAsync(context);
		context.RegisterForDisposal(fake); // Switched off part way through; disposed again at the end if that failed
		await using CatalogFixture fixture = WindowsIppFallbackScenarios.CreateCatalog(WindowsIppFallbackScenarios.PausedPrinterName, fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync("windows:" + WindowsIppFallbackScenarios.PausedPrinterName, context.CancellationToken);

		// While the printer is on, the PDF goes to it, though the queue is paused
		using (FileStream first = TestDocuments.Open(TestDocuments.ThreePages))
		{
			IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(first), PrintOptions.Default, context.CancellationToken);
			PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
			context.CheckEqual("while it is on, the printer completes the job", PrintJobState.Completed, status.State);
		}

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("while it is on, the printer gets the job", 1, fake.GetRequests(IppOperation.PrintJob).Count);
		await fake.DisposeAsync();
		context.Note("The IPP printer is switched off.");

		// Then the same queue's next PDF goes to the spooler: the cached printer fails to connect, and the document
		// is sent from its start
		await WindowsIppFallbackScenarios.SubmitToSpoolerAsync(context, printer);
	}

	/// <summary>
	/// Runs <see cref="PasswordRequired"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PasswordRequiredAsync(ScenarioContext context)
	{
		FakePrinterOptions options = new FakePrinterOptions { RequiredCredentials = new NetworkCredential("latiprimere", "test-password") };
		await using FakePrinter fake = await FakePrinters.StartAsync(context, options);
		await using CatalogFixture fixture = WindowsIppFallbackScenarios.CreateCatalog(WindowsIppFallbackScenarios.PausedPrinterName, fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync("windows:" + WindowsIppFallbackScenarios.PausedPrinterName, context.CancellationToken);

		await WindowsIppFallbackScenarios.SubmitToSpoolerAsync(context, printer);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.Check("the printer asked for a password", fake.Requests.Any(request => request.HttpStatus == 401));
		context.Check("the printer got no job", fake.GetRequests(IppOperation.PrintJob).All(request => request.StatusCode is null));
	}

	/// <summary>
	/// Runs <see cref="PasswordForJobs"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PasswordForJobsAsync(ScenarioContext context)
	{
		FakePrinterOptions options = new FakePrinterOptions
		{
			RequiredCredentials            = new NetworkCredential("latiprimere", "test-password"),
			RequiresCredentialsForJobsOnly = true
		};

		await using FakePrinter fake = await FakePrinters.StartAsync(context, options);
		await using CatalogFixture fixture = WindowsIppFallbackScenarios.CreateCatalog(WindowsIppFallbackScenarios.PausedPrinterName, fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync("windows:" + WindowsIppFallbackScenarios.PausedPrinterName, context.CancellationToken);

		await WindowsIppFallbackScenarios.SubmitToSpoolerAsync(context, printer);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.Check("the printer was asked what it takes", fake.GetRequests(IppOperation.GetPrinterAttributes).Count > 0);
		context.Check("it was never refused for a password, as nothing was sent that needed one", fake.Requests.All(request => request.HttpStatus != 401));
		context.CheckEqual("no Validate-Job was sent", 0, fake.GetRequests(IppOperation.ValidateJob).Count);
		context.CheckEqual("no Print-Job was sent", 0, fake.GetRequests(IppOperation.PrintJob).Count);
	}

	/// <summary>
	/// Runs <see cref="RefusalThrown"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task RefusalThrownAsync(ScenarioContext context)
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("The Windows print system needs Windows.");
		}

		// A printer that prints one copy at a time refuses a job asking for two. Were the spooler to print it as well,
		// the file port's file would change
		FakePrinterProfile profile = (await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken)).SetRange("copies-supported", 1, 1);
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = WindowsIppFallbackScenarios.CreateCatalog(WindowsIppFallbackScenarios.RawPrinterName, fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync("windows:" + WindowsIppFallbackScenarios.RawPrinterName, context.CancellationToken);
		string outputFile = Path.Combine(context.Targets.WindowsOutputDirectory!, WindowsIppFallbackScenarios.RawOutputFileName); // Needed, so set

		using SharedPrinterLock printerLock = new SharedPrinterLock(WindowsIppFallbackScenarios.RawPrinterName);
		DateTime before = File.Exists(outputFile) ? File.GetLastWriteTimeUtc(outputFile) : DateTime.MinValue;
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintException? exception = null;

		try
		{
			await printer.SubmitAsync(PrintContent.FromPdf(document), new PrintOptions { Copies = 2 }, context.CancellationToken);
		}
		catch (PrintException caught)
		{
			exception = caught;
		}

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Require("submitting throws a PrintException", exception is not null);
		context.Note("Message: " + exception!.Message);
		context.CheckEqual("its error code is the printer's 0x040B", (int?)0x040B, exception.NativeErrorCode);
		context.CheckEqual("the printer checked the job once, with Validate-Job (ADR 0033)", 1, fake.GetRequests(IppOperation.ValidateJob).Count);
		context.CheckEqual("so it got no Print-Job, nor the document", 0, fake.GetRequests(IppOperation.PrintJob).Count);

		// Give a spooler job, if there wrongly were one, time to reach the port
		await Task.Delay(TimeSpan.FromSeconds(3), context.CancellationToken);
		DateTime after = File.Exists(outputFile) ? File.GetLastWriteTimeUtc(outputFile) : DateTime.MinValue;
		context.CheckEqual("the spooler didn't print it", before, after);
	}

	/// <summary>
	/// Sends the three-page PDF to a test printer and checks it went to the spooler's path: the only place that
	/// refuses a PDF for the text-only driver, before anything is queued.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <param name="printer">The test printer, mapped to an IPP printer.</param>
	/// <returns>A task that completes when the refusal has been checked.</returns>
	private static async Task SubmitToSpoolerAsync(ScenarioContext context, IPrinter printer)
	{
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		NotSupportedException? refusal = null;
		IPrintJob? job = null;

		try
		{
			job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		}
		catch (NotSupportedException exception)
		{
			refusal = exception;
		}

		if (job is not null)
		{
			// Never leave a job in the test printer's queue
			context.Note($"Job {job.Id} was queued; cancelling it.");
			await job.CancelAsync(CancellationToken.None);
		}

		context.Check
		(
			"the spooler's path refuses it for the text-only driver",
			refusal?.Message.Contains("Generic / Text Only", StringComparison.Ordinal) == true,
			refusal?.Message ?? (job is null ? "nothing thrown" : $"queued as job {job.Id}")
		);
	}

	/// <summary>
	/// Builds a catalog over the Windows print system with a test printer mapped to an IPP printer.
	/// </summary>
	/// <param name="printerName">The test printer's name in Windows.</param>
	/// <param name="printerUri">The IPP printer.</param>
	/// <returns>The catalog fixture. The caller disposes it.</returns>
	private static CatalogFixture CreateCatalog(string printerName, Uri printerUri)
	{
		return new CatalogFixture
		(
			services => services.AddLatiprimerePdfium().AddLatiprimereWindows
			(
				options =>
				{
					options.PrintPdfOverIpp = true;
					options.MaxRenderDpi = WindowsIppFallbackScenarios.RenderDpi;
					options.IppPrinterUris[printerName] = printerUri;
				}
			)
		);
	}
}
