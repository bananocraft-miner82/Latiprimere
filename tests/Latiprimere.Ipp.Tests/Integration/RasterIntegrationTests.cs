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
	private static readonly MediaSize A4 = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimetres = 210, HeightMillimetres = 297 };

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
		PrintJobResponse response = await client.PrintJobAsync(request, IppVendorAttributes.None, TestContext.Current.CancellationToken);
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
			.Build();

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
}
