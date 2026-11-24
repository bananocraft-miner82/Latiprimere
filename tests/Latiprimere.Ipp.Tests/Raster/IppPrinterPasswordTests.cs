using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Raster;

/// <summary>
/// Password-protected PDFs over IPP: sent with their password to a printer that takes one, as raster otherwise
/// (ADR 0054).
/// </summary>
public sealed class IppPrinterPasswordTests
{
	private const string Password = "test-password";                                      // The PDF's password
	private static readonly Uri PrinterUri = new Uri("ipp://192.168.1.20:631/ipp/print"); // Printer under test

	[Fact]
	public async Task GivenAPrinterTakingPasswordsWhenAProtectedPdfIsSubmittedThenThePasswordGoesWithTheDocumentOnly()
	{
		// Arrange: it takes PDF, and passwords of up to 1,023 octets
		FakeIppClient client = IppPrinterPasswordTests.CreateClient(new IppAttribute(Tag.Integer, "document-password-supported", 1023));
		QuadrantRasterizer rasterizer = new QuadrantRasterizer(pageCount: 1);
		IppPrinter printer = IppPrinterPasswordTests.CreatePrinter(client, rasterizer, ["application/pdf", "image/pwg-raster"]);

		// Act
		await printer.SubmitAsync(IppPrinterPasswordTests.ProtectedPdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert: sent as PDF, with the password among the operation attributes, and Validate-Job without it
		Assert.Equal("application/pdf", client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		IppAttribute password = Assert.Single(client.LastVendorAttributes!.Operation, attribute => attribute.Name == "document-password");
		Assert.Equal(Tag.OctetStringWithAnUnspecifiedFormat, password.Tag);
		Assert.Equal(System.Text.Encoding.UTF8.GetBytes(IppPrinterPasswordTests.Password), Assert.IsType<byte[]>(password.Value));
		Assert.DoesNotContain(client.LastValidateJobVendorAttributes!.Operation, attribute => attribute.Name == "document-password");
		Assert.Empty(rasterizer.Opened);
	}

	[Fact]
	public async Task GivenAPrinterTakingNoPasswordsWhenAProtectedPdfIsSubmittedThenItGoesAsRasterRenderedWithThePassword()
	{
		// Arrange: it takes PDF and PWG raster, but no passwords
		FakeIppClient client = IppPrinterPasswordTests.CreateClient(new IppAttribute(Tag.Integer, "document-password-supported", 0));
		QuadrantRasterizer rasterizer = new QuadrantRasterizer(pageCount: 1);
		IppPrinter printer = IppPrinterPasswordTests.CreatePrinter(client, rasterizer, ["application/pdf", "image/pwg-raster"]);

		// Act
		await printer.SubmitAsync(IppPrinterPasswordTests.ProtectedPdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("image/pwg-raster", client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Equal(IppPrinterPasswordTests.Password, Assert.Single(rasterizer.OpenedWith)?.Password);
		Assert.DoesNotContain(client.LastVendorAttributes!.Operation, attribute => attribute.Name == "document-password");
	}

	[Fact]
	public async Task GivenAPasswordLongerThanThePrinterTakesWhenSubmittedThenItGoesAsRaster()
	{
		// Arrange: 13 octets of password, and a printer taking 8
		FakeIppClient client = IppPrinterPasswordTests.CreateClient(new IppAttribute(Tag.Integer, "document-password-supported", 8));
		QuadrantRasterizer rasterizer = new QuadrantRasterizer(pageCount: 1);
		IppPrinter printer = IppPrinterPasswordTests.CreatePrinter(client, rasterizer, ["application/pdf", "image/pwg-raster"]);

		// Act
		await printer.SubmitAsync(IppPrinterPasswordTests.ProtectedPdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("image/pwg-raster", client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
	}

	[Fact]
	public async Task GivenAPrinterWhoseFormatsAreNotKnownWhenAProtectedPdfIsSubmittedWithoutPasswordSupportThenNotSupported()
	{
		// Arrange: as a CUPS queue, whose formats aren't checked and which has no rasteriser
		FakeIppClient client = IppPrinterPasswordTests.CreateClient();
		IppPrintContextBuilder builder = new IppPrintContextBuilder().WithClient(client).WithDocumentFormats(IppDocumentFormats.Standard);
		IppPrinter printer = new IppPrinter(builder.Build(), IppPrinterPasswordTests.PrinterUri, new PrinterInfo { Id = "cups:x", Name = "x" });

		// Act
		Func<Task> submit = () => printer.SubmitAsync(IppPrinterPasswordTests.ProtectedPdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert: refused before anything is sent, without the password in the message
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("password-protected", exception.Message, StringComparison.Ordinal);
		Assert.DoesNotContain(IppPrinterPasswordTests.Password, exception.Message, StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenAPdfWithoutPasswordWhenSubmittedToAPrinterTakingPdfThenPasswordSupportIsNotAskedFor()
	{
		// Arrange
		FakeIppClient client = IppPrinterPasswordTests.CreateClient(new IppAttribute(Tag.Integer, "document-password-supported", 1023));
		IppPrinter printer = IppPrinterPasswordTests.CreatePrinter(client, new QuadrantRasterizer(pageCount: 1), ["application/pdf"]);
		using MemoryStream pdf = new MemoryStream("%PDF-1.7"u8.ToArray());

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(pdf), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(0, client.AttributeListRequestCount);
		Assert.DoesNotContain(client.LastVendorAttributes!.Operation, attribute => attribute.Name == "document-password");
	}

	/// <summary>
	/// Creates a client with a printer that takes PWG raster in grey, and the given attributes too.
	/// </summary>
	/// <param name="attributes">More attributes, such as <c>document-password-supported</c>.</param>
	/// <returns>The client.</returns>
	private static FakeIppClient CreateClient(params IppAttribute[] attributes)
	{
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter(IppPrinterPasswordTests.PrinterUri, new PrinterDescriptionAttributes());
		client.SetAttributeList(IppPrinterPasswordTests.PrinterUri, [.. RasterJobPlannerTests.PwgAttributes("sgray_8"), .. attributes]);

		return client;
	}

	/// <summary>
	/// Creates the printer under test, as direct IPP does, listing Validate-Job.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="rasterizer">The PDF rasteriser.</param>
	/// <param name="formats">The formats the printer takes.</param>
	/// <returns>The printer.</returns>
	private static IppPrinter CreatePrinter(FakeIppClient client, QuadrantRasterizer rasterizer, string[] formats)
	{
		IppPrintContextBuilder builder = new IppPrintContextBuilder()
			.WithClient(client)
			.WithDocumentFormats(IppDocumentFormats.Standard)
			.WithPdfRasterizer(rasterizer)
			.WithValidateJobs(true);

		return new IppPrinter
		(
			builder.Build(),
			IppPrinterPasswordTests.PrinterUri,
			new PrinterInfo { Id = "ipp:x", Name = "x" },
			formats,
			[IppOperation.PrintJob, IppOperation.ValidateJob]
		);
	}

	/// <summary>
	/// A stand-in password-protected PDF, which the quadrant rasteriser doesn't read.
	/// </summary>
	/// <returns>The content.</returns>
	private static PrintContent ProtectedPdf()
	{
		return PrintContent.FromPdf(new MemoryStream("%PDF-1.7"u8.ToArray()), IppPrinterPasswordTests.Password);
	}
}
