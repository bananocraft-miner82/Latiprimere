using Latiprimere.Ipp.Raster;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;
using Latiprimere.Testing.Fixtures;
using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Integration;

/// <summary>
/// PWG raster from the encoder, printed by <c>ippeveprinter</c>, which reads it as a real printer would and aborts a
/// job it can't (ADR 0034).
/// </summary>
[Trait("Category", "Integration")]
[Trait("OS", "Any")]
public sealed class RasterIntegrationTests
{
	/// <summary>How long to resend a job the printer turns away as busy: CI runs each target framework's tests at
	/// once against the one ippeveprinter, which takes one job at a time.</summary>
	private static readonly TimeSpan BusyRetryDuration = IppNetworkOptions.DefaultBusyRetryDuration;

	/// <summary>The wait between resends of a job turned away as busy.</summary>
	private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromSeconds(1);

	private static readonly MediaSize A4 = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimeters = 210, HeightMillimeters = 297 };

	[Theory]
	[InlineData((int)RasterColorSpace.Gray8, 300)]
	[InlineData((int)RasterColorSpace.Black1, 600)]
	public async Task GivenPwgRasterFromAPdfWhenPrintedToIppeveprinterThenTheJobCompletes(int colorSpace, int dpi)
	{
		// Arrange — the container's printer takes sgray_8 and black_1 at 300 and 600 dpi (provided by InlineData above)
		Uri? printerUri = IntegrationEnvironment.IppPrinterUri;
		Assert.SkipWhen(printerUri is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.IppPrinterVariable));

		RasterSettings settings = new RasterSettings
		{
			Format     = RasterFormat.PwgRaster,
			ColorSpace = (RasterColorSpace)colorSpace,
			Dpi        = dpi,
			Media      = RasterIntegrationTests.A4
		};

		using MemoryStream raster = new MemoryStream();

		using (FileStream file = TestDocuments.Open(TestDocuments.ThreePages))
		using (IRasterDocument document = new PdfiumRasterizer().Open(file))
		{
			RasterDocumentEncoder.Encode(document, settings, raster, TestContext.Current.CancellationToken);
		}

		raster.Position = 0;
		using SharpIppClientAdapter client = new SharpIppClientAdapter(IppHttpClientFactory.CreateForTcp(IppNetworkOptions.DefaultConnectTimeout));
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).WithDocumentFormats(IppDocumentFormats.Standard).Build();

		PrintJobRequest request = new PrintJobRequest
		{
			Document = raster,
			OperationAttributes = new PrintJobOperationAttributes
			{
				PrinterUri         = new IppValue<Uri>(printerUri!),
				RequestingUserName = new StringWithLanguage("latiprimere-tests"),
				DocumentFormat     = new IppValue<SharpIpp.Protocol.Models.DocumentFormat>(new SharpIpp.Protocol.Models.DocumentFormat("image/pwg-raster")),
				JobName            = new StringWithLanguage($"Latiprimere raster {settings.ColorSpace} {dpi} dpi")
			}
		};

		// Act
		PrintJobResponse response = await RasterIntegrationTests.PrintJobWhenNotBusyAsync(client, request, raster);
		int jobId = response.JobAttributes?.JobId ?? 0;
		IppPrintJob job = new IppPrintJob(context, printerUri!, "ipp:" + printerUri!.AbsoluteUri, jobId);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert — Aborted would mean it couldn't read the raster
		Assert.True(jobId > 0);
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Fact]
	public async Task GivenAPrinterTakenNotToTakePdfWhenAPdfIsSubmittedThenIppeveprinterPrintsItsStreamedRaster()
	{
		// Arrange — the container's printer takes PDF too, so it's given a list without PDF: the job then goes as
		// raster through the real client, streamed and gzipped as for a printer that can't take PDF (ADR 0035)
		Uri? printerUri = IntegrationEnvironment.IppPrinterUri;
		Assert.SkipWhen(printerUri is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.IppPrinterVariable));

		using SharpIppClientAdapter client = new SharpIppClientAdapter(IppHttpClientFactory.CreateForTcp(IppNetworkOptions.DefaultConnectTimeout));
		IppPrintContext context = new IppPrintContextBuilder()
			.WithClient(client)
			.WithDocumentFormats(IppDocumentFormats.Standard)
			.WithPdfRasterizer(new PdfiumRasterizer())
			.WithValidateJobs(true)
			.WithBusyRetryDuration(RasterIntegrationTests.BusyRetryDuration)
			.Build() with { BusyRetryFirstDelay = RasterIntegrationTests.BusyRetryDelay, BusyRetryMaxDelay = RasterIntegrationTests.BusyRetryDelay };

		IppPrinter printer = new IppPrinter
		(
			context,
			printerUri!,
			new PrinterInfo { Id = "ipp:" + printerUri!.AbsoluteUri, Name = "ippeveprinter" },
			["application/octet-stream", "image/pwg-raster"],
			[IppOperation.PrintJob, IppOperation.ValidateJob]
		);

		using FileStream file = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { JobName = "Latiprimere raster PDF", MediaSize = RasterIntegrationTests.A4 };

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(file), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert — Aborted would mean it couldn't read the raster
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	/// <summary>
	/// Sends Print-Job, sending it again from the start of the document while the printer is busy with another job,
	/// as <see cref="IppPrinter"/> does.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="request">The request, with its document.</param>
	/// <param name="document">The request's document, rewound before each resend.</param>
	/// <returns>The printer's response.</returns>
	private static async Task<PrintJobResponse> PrintJobWhenNotBusyAsync(SharpIppClientAdapter client, PrintJobRequest request, MemoryStream document)
	{
		const int serverErrorBusy = 0x0507;
		DateTime deadline = DateTime.UtcNow + RasterIntegrationTests.BusyRetryDuration;

		while (true)
		{
			try
			{
				return await client.PrintJobAsync(request, IppVendorAttributes.None, TestContext.Current.CancellationToken);
			}
			catch (PrintException exception) when (exception.NativeErrorCode == serverErrorBusy && DateTime.UtcNow < deadline)
			{
				await Task.Delay(RasterIntegrationTests.BusyRetryDelay, TestContext.Current.CancellationToken);
				document.Position = 0;
			}
		}
	}
}
