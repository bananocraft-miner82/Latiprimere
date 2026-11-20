using System.IO.Compression;
using System.Net;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Testing.Raster;
using Microsoft.Extensions.DependencyInjection;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// The Windows print system sending PDFs straight to a queue's IPP printer (ADR 0027), with the scripted printer
/// standing in for it.
/// </summary>
public static class WindowsIppRouteScenarios
{
	/// <summary>
	/// A PDF to a Windows queue mapped to an IPP printer goes to the printer, unrendered, and reports its outcome.
	/// </summary>
	public static readonly Scenario PdfToPrinter = new Scenario
	{
		Id = "windows.ipp-route.pdf-to-printer",
		Area = ScenarioArea.Submission,
		Title = "A PDF to Microsoft Print to PDF's queue, mapped to an IPP printer, reaches that printer unchanged under the Windows printer ID, and reports the printer's outcome",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = WindowsIppRouteScenarios.PdfToPrinterAsync
	};

	/// <summary>
	/// A PDF to a queue whose IPP printer takes Apple raster but not PDF goes to it as Apple raster (ADR 0036).
	/// </summary>
	public static readonly Scenario RasterToPrinter = new Scenario
	{
		Id = "windows.ipp-route.raster-to-printer",
		Area = ScenarioArea.Submission,
		Title = "A PDF to Microsoft Print to PDF's queue, mapped to a printer with the HP ENVY's attributes, which takes Apple raster but not PDF, reaches it as three gzipped Apple raster pages at MaxRenderDpi, under the Windows printer ID, and completes",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = WindowsIppRouteScenarios.RasterToPrinterAsync
	};

	/// <summary>
	/// A PDF to a queue whose IPP printer is reached over IPPS, with its certificate pinned and a password, goes to it
	/// there (ADR 0042).
	/// </summary>
	public static readonly Scenario PasswordOverIpps = new Scenario
	{
		Id = "windows.ipp-route.password-over-ipps",
		Area = ScenarioArea.Submission,
		Title = "A PDF to Microsoft Print to PDF's queue, mapped to an ipps:// printer with a self-signed certificate that asks for a password, reaches it with the certificate pinned and the password in WindowsOptions, every request carrying it, and completes",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = WindowsIppRouteScenarios.PasswordOverIppsAsync
	};

	/// <summary>
	/// Runs <see cref="PdfToPrinter"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PdfToPrinterAsync(ScenarioContext context)
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("The Windows print system needs Windows.");
		}

		await using FakePrinter fake = await FakePrinters.StartAsync(context);

		await using CatalogFixture fixture = new CatalogFixture
		(
			services => services.AddLatiprimerePdfium().AddLatiprimereWindows
			(
				options =>
				{
					options.PrintPdfOverIpp = true;
					options.IppPrinterUris[TestPrinters.MicrosoftPrintToPdfName] = fake.PrinterUri;
				}
			)
		);

		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);
		byte[] pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", TestDocuments.ThreePages), context.CancellationToken);
		using MemoryStream document = new MemoryStream(pdf);

		// Held in case the job falls back to the driver, which can't share with another process
		using SharedPrinterLock printerLock = new SharedPrinterLock(TestPrinters.MicrosoftPrintToPdfName);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), new PrintOptions { Copies = 2 }, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.CheckEqual("the job keeps the Windows printer ID", TestPrinters.MicrosoftPrintToPdf, job.PrinterId);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);
		context.Check("its status is the printer's, not the spooler's", status.StateMessage?.Contains("Windows does not record", StringComparison.Ordinal) != true, status.StateMessage ?? "no message");

		IReadOnlyList<RecordedRequest> printJobs = fake.GetRequests(IppOperation.PrintJob);
		context.Require("the printer received one Print-Job", printJobs.Count == 1, $"{printJobs.Count}");
		context.CheckEqual("as application/pdf", "application/pdf", printJobs[0].Find("document-format")?.Value);
		context.CheckEqual("with copies 2", "2", printJobs[0].Find("copies")?.Value);
		context.Check("the PDF arrives unchanged, not rendered", printJobs[0].Document.AsSpan().SequenceEqual(pdf), $"{printJobs[0].Document.Length} bytes received, {pdf.Length} sent");
	}

	/// <summary>
	/// Runs <see cref="PasswordOverIpps"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PasswordOverIppsAsync(ScenarioContext context)
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("The Windows print system needs Windows.");
		}

		NetworkCredential credentials = new NetworkCredential("latiprimere", "test-password");
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { UseTls = true, RequiredCredentials = credentials });
		context.Note($"Its certificate's fingerprint: {fake.CertificateFingerprint}");

		await using CatalogFixture fixture = new CatalogFixture
		(
			services => services.AddLatiprimerePdfium().AddLatiprimereWindows
			(
				options =>
				{
					options.PrintPdfOverIpp = true;
					options.IppPrinterUris[TestPrinters.MicrosoftPrintToPdfName] = fake.PrinterUri;
					options.IppTrustedCertificateFingerprints.Add(fake.CertificateFingerprint!);
					options.IppPrinterCredentials[fake.PrinterUri] = credentials;
				}
			)
		);

		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		// Held in case the job falls back to the driver, which can't share with another process
		using SharedPrinterLock printerLock = new SharedPrinterLock(TestPrinters.MicrosoftPrintToPdfName);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.CheckEqual("the printer's URI is ipps", "ipps", fake.PrinterUri.Scheme);
		context.Check("the job is the printer's", job.Id.StartsWith("ipp:", StringComparison.Ordinal), job.Id);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);
		context.CheckEqual("the printer received one Print-Job", 1, fake.GetRequests(IppOperation.PrintJob).Count);
		context.Check("every request carried the password", fake.Requests.Count > 0 && fake.Requests.All(request => request.HasAuthorization));
	}

	/// <summary>
	/// Runs <see cref="RasterToPrinter"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task RasterToPrinterAsync(ScenarioContext context)
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("The Windows print system needs Windows.");
		}

		FakePrinterProfile profile = await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken);
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });

		await using CatalogFixture fixture = new CatalogFixture
		(
			services => services.AddLatiprimerePdfium().AddLatiprimereWindows
			(
				options =>
				{
					options.PrintPdfOverIpp = true;
					options.MaxRenderDpi = 300;
					options.IppPrinterUris[TestPrinters.MicrosoftPrintToPdfName] = fake.PrinterUri;
				}
			)
		);

		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		// Held in case the job falls back to the driver, which can't share with another process
		using SharedPrinterLock printerLock = new SharedPrinterLock(TestPrinters.MicrosoftPrintToPdfName);

		PrintOptions options = new PrintOptions { MediaSize = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimeters = 210, HeightMillimeters = 297 } };
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.CheckEqual("the job keeps the Windows printer ID", TestPrinters.MicrosoftPrintToPdf, job.PrinterId);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		IReadOnlyList<RecordedRequest> printJobs = fake.GetRequests(IppOperation.PrintJob);
		context.Require("the printer received one Print-Job", printJobs.Count == 1, $"{printJobs.Count}");
		context.CheckEqual("as image/urf", "image/urf", printJobs[0].Find("document-format")?.Value);
		context.CheckEqual("gzipped", "gzip", printJobs[0].Find("compression")?.Value);

		using GZipStream gzip = new GZipStream(new MemoryStream(printJobs[0].Document), CompressionMode.Decompress);
		using MemoryStream raster = new MemoryStream();
		await gzip.CopyToAsync(raster, context.CancellationToken);
		RasterDocument pages = RasterDocumentReader.Read(raster.ToArray());

		context.CheckEqual("three pages", 3, pages.Pages.Count);
		context.Check("each A4 at 300 dpi, the limit set", pages.Pages.All(page => (page.Width, page.Height, page.DpiX) == (2480, 3507, 300)));
	}
}
