using System.Text;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Ipp.Tests.Documents;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests;

/// <summary>
/// An IPP printer's details, identifying it, and its test page (ADR 0059).
/// </summary>
public sealed class IppPrinterDetailsTests
{
	private static readonly Uri PrinterUri = new Uri("ipp://192.168.1.20:631/ipp/print"); // Printer under test

	[Fact]
	public async Task GivenAPrinterWhenItsDetailsAreReadThenTheyAreAskedForInOneRawRequest()
	{
		// Arrange
		FakeIppClient client = IppPrinterDetailsTests.CreateClient
		([
			new IppAttribute(Tag.Uri, "printer-more-info", "http://192.168.1.20/"),
			new IppAttribute(Tag.Keyword, "identify-actions-supported", "flash"),
		]);

		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(client);

		// Act
		PrinterDetails details = await printer.GetDetailsAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(("ipp:x", new Uri("http://192.168.1.20/")), (details.PrinterId, details.WebPage));
		Assert.Equal([IdentifyAction.Flash], details.IdentifyActions);
		Assert.Equal(1, client.AttributeListRequestCount);
		Assert.Empty(client.GetPrinterAttributesRequests);
	}

	[Fact]
	public async Task GivenAPrinterThatIsGoneWhenItsDetailsAreReadThenThrowsPrintException()
	{
		// Arrange — a client with no printer at that URI
		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(new FakeIppClient());

		// Act
		Func<Task> read = () => printer.GetDetailsAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintException>(read);
	}

	[Fact]
	public async Task GivenAMessageWhenThePrinterIsIdentifiedThenIdentifyPrinterIsSentWithIt()
	{
		// Arrange
		FakeIppClient client = IppPrinterDetailsTests.CreateClient([]);
		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(client);

		// Act
		await printer.IdentifyAsync("This is Office Laser", TestContext.Current.CancellationToken);

		// Assert — no actions, so the printer does what it does by default
		IdentifyPrinterOperationAttributes? sent = Assert.Single(client.IdentifyPrinterRequests).OperationAttributes;
		Assert.Equal(IppPrinterDetailsTests.PrinterUri, sent?.PrinterUri?.Value);
		Assert.Equal("This is Office Laser", sent?.Message?.Value);
		Assert.False(string.IsNullOrEmpty(sent?.RequestingUserName?.Value));
		Assert.Null(sent?.IdentifyActions);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task GivenNoMessageWhenThePrinterIsIdentifiedThenNoneIsSent(string? message)
	{
		// Arrange (provided by InlineData above)
		FakeIppClient client = IppPrinterDetailsTests.CreateClient([]);
		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(client);

		// Act
		await printer.IdentifyAsync(message, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(Assert.Single(client.IdentifyPrinterRequests).OperationAttributes?.Message);
	}

	[Theory]
	[InlineData(127, true)]
	[InlineData(128, false)]
	public async Task GivenAMessageOfSomeBytesWhenThePrinterIsIdentifiedThenOnlyOneOfAtMost127IsSent(int bytes, bool sent)
	{
		// Arrange — é is two bytes in UTF-8, so the limit is in bytes, not characters
		FakeIppClient client = IppPrinterDetailsTests.CreateClient([]);
		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(client);
		string message = new string('é', bytes / 2) + new string('x', bytes % 2);
		Assert.Equal(bytes, Encoding.UTF8.GetByteCount(message));

		// Act
		Func<Task> identify = () => printer.IdentifyAsync(message, TestContext.Current.CancellationToken);

		// Assert
		if (sent)
		{
			await identify();
			Assert.Single(client.IdentifyPrinterRequests);
		}
		else
		{
			ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(identify);
			Assert.Equal("message", exception.ParamName);
			Assert.Empty(client.IdentifyPrinterRequests);
		}
	}

	[Fact]
	public async Task GivenAPrinterThatDoesNotTakeIdentifyPrinterWhenIdentifiedThenThrowsNotSupported()
	{
		// Arrange
		FakeIppClient client = IppPrinterDetailsTests.CreateClient([]);
		client.IdentifyFailureToThrow = new PrintException("Not supported.") { NativeErrorCode = (int)IppStatusCode.ServerErrorOperationNotSupported };
		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(client);

		// Act
		Func<Task> identify = () => printer.IdentifyAsync(null, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(identify);
		Assert.Contains("ipp://192.168.1.20:631/ipp/print", exception.Message, StringComparison.Ordinal);
		Assert.IsType<PrintException>(exception.InnerException);
	}

	[Fact]
	public async Task GivenAPrinterRefusingIdentifyPrinterForAnotherReasonWhenIdentifiedThenThePrintExceptionIsThrown()
	{
		// Arrange
		FakeIppClient client = IppPrinterDetailsTests.CreateClient([]);
		client.IdentifyFailureToThrow = new PrintException("Forbidden.") { NativeErrorCode = (int)IppStatusCode.ClientErrorForbidden };
		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(client);

		// Act
		Func<Task> identify = () => printer.IdentifyAsync(null, TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintException>(identify);
	}

	[Fact]
	public async Task GivenAPrinterTakingPdfWhenATestPageIsPrintedThenAPdfOnItsDefaultPaperIsSentNamingIt()
	{
		// Arrange — the printer's address carries a password, which the page doesn't show
		Uri printerUri = new Uri("ipp://print:secret@192.168.1.20:631/ipp/print");
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(printerUri, new PrinterDescriptionAttributes());
		client.SetAttributeList(printerUri, [new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm")]);
		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(client, printerUri);

		// Act
		IPrintJob job = await printer.PrintTestPageAsync(PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(job);
		PrintJobRequest request = Assert.IsType<PrintJobRequest>(client.LastPrintJobRequest);
		Assert.Equal("application/pdf", request.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Equal("Latiprimere test page", request.OperationAttributes?.JobName?.Value);

		byte[] pdf = Assert.Single(client.ReceivedDocuments);
		Assert.Contains("/MediaBox [0 0 595.2756 841.8898]", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
		List<string> lines = Assert.Single(PdfTextLines.Read(pdf)).Select(line => line.Text).ToList();
		Assert.Contains("Address        ipp://192.168.1.20:631/ipp/print", lines);
		Assert.Contains("Print system   Network printer over IPP", lines);
		Assert.DoesNotContain(lines, line => line.Contains("secret", StringComparison.Ordinal));
	}

	[Fact]
	public async Task GivenAJobNameAndPaperWhenATestPageIsPrintedThenTheyAreKept()
	{
		// Arrange
		FakeIppClient client = IppPrinterDetailsTests.CreateClient([]);
		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(client);
		MediaSize letter = Latiprimere.Ipp.Mapping.PwgMediaNames.ToMediaSize("na_letter_8.5x11in")!;

		// Act
		await printer.PrintTestPageAsync(new PrintOptions { JobName = "Check", MediaSize = letter }, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("Check", client.LastPrintJobRequest?.OperationAttributes?.JobName?.Value);
		Assert.Contains("/MediaBox [0 0 612 792]", Encoding.Latin1.GetString(Assert.Single(client.ReceivedDocuments)), StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenAPrinterTakingNeitherPdfNorRasterWhenATestPageIsPrintedThenThrowsNotSupportedAndSendsNothing()
	{
		// Arrange
		FakeIppClient client = IppPrinterDetailsTests.CreateClient([]);
		IppPrinter printer = IppPrinterDetailsTests.CreatePrinter(client, IppPrinterDetailsTests.PrinterUri, ["application/octet-stream"]);

		// Act
		Func<Task> print = () => printer.PrintTestPageAsync(PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<NotSupportedException>(print);
		Assert.Empty(client.ReceivedDocuments);
	}

	/// <summary>
	/// Creates a client with the printer, and the raw attributes it reports.
	/// </summary>
	/// <param name="attributes">The raw attributes.</param>
	/// <returns>The client.</returns>
	private static FakeIppClient CreateClient(IReadOnlyList<IppAttribute> attributes)
	{
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter(IppPrinterDetailsTests.PrinterUri, new PrinterDescriptionAttributes());
		client.SetAttributeList(IppPrinterDetailsTests.PrinterUri, [.. attributes, new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm")]);

		return client;
	}

	/// <summary>
	/// Creates the printer under test, as direct IPP does.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="printerUri">Its URI, or <see langword="null"/> for the usual one.</param>
	/// <param name="formats">The formats it takes, or <see langword="null"/> for PDF.</param>
	/// <returns>The printer.</returns>
	private static IppPrinter CreatePrinter(FakeIppClient client, Uri? printerUri = null, string[]? formats = null)
	{
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).WithDocumentFormats(IppDocumentFormats.Standard).Build();

		return new IppPrinter(context, printerUri ?? IppPrinterDetailsTests.PrinterUri, new PrinterInfo { Id = "ipp:x", Name = "x" }, formats ?? ["application/pdf"]);
	}
}
