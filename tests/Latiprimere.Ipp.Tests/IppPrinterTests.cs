using Latiprimere.Ipp.Mapping;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Testing.Fakes;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppPrinterTests
{
	private const int ServerErrorBusy = 0x0507; // IPP status for a printer working on another job

	private static readonly Uri PrinterUri = new Uri("ipp://localhost:631/printers/Office"); // Printer under test

	[Fact]
	public async Task GivenPdfAndOptionsWhenSubmitThenSendsPrintJobWithDocumentAndAttributes()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient { PrintJobId = 42 };
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
		PrintOptions options = new PrintOptions { JobName = "Invoice", Copies = 2 };

		// Act
		IPrintJob job = await printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			options,
			TestContext.Current.CancellationToken
		);

		// Assert
		PrintJobRequest request = Assert.IsType<PrintJobRequest>(client.LastPrintJobRequest);
		Assert.Same(document, request.Document);
		Assert.Equal(IppPrinterTests.PrinterUri, request.OperationAttributes?.PrinterUri?.Value);
		Assert.Equal("tester", request.OperationAttributes?.RequestingUserName?.Value);
		Assert.Equal("application/pdf", request.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Equal("Invoice", request.OperationAttributes?.JobName?.Value);
		Assert.Equal(2, request.JobTemplateAttributes?.Copies?.Value);
		Assert.Equal("42", job.Id);
		Assert.Equal("cups:Office", job.PrinterId);
	}

	[Fact]
	public async Task GivenColourSizeAndTrayWhenSubmitThenSendsThemAsJobAttributes()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintOptions options = new PrintOptions
		{
			ColorMode = ColorMode.Color,
			MediaSize = new MediaSize
			{
				Name              = "iso_a5_148x210mm",
				DisplayName       = "A5",
				WidthMillimeters  = 148,
				HeightMillimeters = 210
			},
			InputTray = new InputTray { Id = "manual", Name = "Manual" }
		};

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		PrintJobRequest request = Assert.IsType<PrintJobRequest>(client.LastPrintJobRequest);
		Assert.Equal("color", request.JobTemplateAttributes?.PrintColorMode?.Value.Value);
		Assert.Equal("manual", request.JobTemplateAttributes?.MediaCol?.Value.MediaSource?.Value.Value);
		Assert.Equal(14800, request.JobTemplateAttributes?.MediaCol?.Value.MediaSize?.Value.XDimension?.Value.Lower);
	}

	[Fact]
	public async Task GivenVendorOptionsWhenSubmitThenSendsThoseThisPrintSystemReads()
	{
		// Arrange — a network printer reads ipp: keys only
		FakeIppClient client = new FakeIppClient();
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintOptions options = new PrintOptions
		{
			VendorOptions = new Dictionary<string, string>
			{
				["ipp:print-quality"] = "5",
				["cups:fit-to-page"]  = "true",
				["windows:devmode"]   = "AAAA"
			}
		};

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		IppVendorAttributes vendorAttributes = Assert.IsType<IppVendorAttributes>(client.LastVendorAttributes);
		IppAttribute attribute = Assert.Single(vendorAttributes.Job);
		Assert.Equal("print-quality", attribute.Name);
		Assert.Equal(5, attribute.Value);
	}

	[Fact]
	public async Task GivenVendorOptionRepeatingATypedOptionWhenSubmitThenRefusesBeforeSending()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintOptions options = new PrintOptions
		{
			Copies        = 2,
			VendorOptions = new Dictionary<string, string> { ["ipp:copies"] = "3" }
		};

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(submit);
		Assert.Contains("'ipp:copies'", exception.Message, StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenRawContentWhenSubmitThenSendsRawMimeTypeAndNoJobName()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);
		using MemoryStream document = new MemoryStream(new byte[] { 0x1B, 0x40 });

		// Act
		await printer.SubmitAsync
		(
			PrintContent.FromRaw(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		PrintJobRequest request = Assert.IsType<PrintJobRequest>(client.LastPrintJobRequest);
		Assert.Equal("application/vnd.cups-raw", request.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Null(request.OperationAttributes?.JobName);
	}

	[Fact]
	public async Task GivenResponseWithoutJobIdWhenSubmitThenThrowsPrintException()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient { PrintJobId = 0 };
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);
		using MemoryStream document = new MemoryStream();

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		await Assert.ThrowsAsync<PrintException>(submit);
	}

	[Fact]
	public async Task GivenOutputFileWhenSubmitThenThrowsNotSupportedExceptionWithoutSending()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
		PrintOptions options = new PrintOptions { OutputFile = "out.pdf" };

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			options,
			TestContext.Current.CancellationToken
		);

		// Assert
		await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenFormatThePrinterAcceptsWhenSubmitThenSends()
	{
		// Arrange — format names compare without regard to case
		FakeIppClient client = new FakeIppClient();
		IppPrinter printer = IppPrinterTests.CreatePrinter(client, new[] { "Application/PDF" });
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		await printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.NotNull(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenFormatThePrinterDoesNotAcceptWhenSubmitThenThrowsNotSupportedExceptionWithoutSending()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		IppPrinter printer = IppPrinterTests.CreatePrinter(client, new[] { "image/urf" });
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenPrinterWhenGetCapabilitiesThenAsksForCapabilityAttributesAndMapsThem()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppPrinterTests.PrinterUri, new PrinterAttributesBuilder().Build());
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(capabilities);
		GetPrinterAttributesRequest request = Assert.Single(client.GetPrinterAttributesRequests);
		Assert.Equal(IppPrinterTests.PrinterUri, request.OperationAttributes?.PrinterUri?.Value);

		Assert.Equal
		(
			IppRequestedAttributes.CapabilityAttributes,
			request.OperationAttributes?.RequestedAttributes?.Value
		);
	}

	[Fact]
	public async Task GivenPrinterWithJobAttributesWhenGetCapabilitiesThenListsThemAsVendorOptions()
	{
		// Arrange — the raw attributes are asked for alongside the typed ones
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppPrinterTests.PrinterUri, new PrinterAttributesBuilder().Build());

		client.SetAttributeList
		(
			IppPrinterTests.PrinterUri,
			[
				new IppAttribute(Tag.Keyword, "job-creation-attributes-supported", "print-quality"),
				new IppAttribute(Tag.Enum, "print-quality-supported", 4),
				new IppAttribute(Tag.Enum, "print-quality-supported", 5),
				new IppAttribute(Tag.Enum, "print-quality-default", 4),
			]
		);

		IppPrinter printer = IppPrinterTests.CreatePrinter(client);

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(1, client.AttributeListRequestCount);
		VendorOption quality = Assert.Single(capabilities.VendorOptions);
		Assert.Equal("ipp:print-quality", quality.Key);
		Assert.Equal(["4", "5"], quality.Choices.Select(choice => choice.Value));
		Assert.Equal("4", quality.DefaultValue);
	}

	[Fact]
	public async Task GivenPrinterThatRefusesTheVendorOptionRequestWhenGetCapabilitiesThenReturnsTheRestAndLogsIt()
	{
		// Arrange — some printers turn away the job-template group; the typed capabilities don't depend on it
		FakeIppClient client = new FakeIppClient
		{
			AttributeListFailureToThrow = new PrintException("client-error-attributes-or-values-not-supported")
		};

		client.AddPrinter(IppPrinterTests.PrinterUri, new PrinterAttributesBuilder().Build());
		RecordingLogger logger = new RecordingLogger();
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).WithLogger(logger).Build();
		IppPrinter printer = new IppPrinter(context, IppPrinterTests.PrinterUri, new PrinterInfo { Id = "cups:Office", Name = "Office" }, null);

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Empty(capabilities.VendorOptions);
		Assert.Single(client.GetPrinterAttributesRequests);
		(Microsoft.Extensions.Logging.LogLevel level, string message, Exception? exception) = Assert.Single(logger.Entries);
		Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, level);
		Assert.Contains(IppPrinterTests.PrinterUri.ToString(), message, StringComparison.Ordinal);
		Assert.IsType<PrintException>(exception);
	}

	[Theory]
	[InlineData(0x040B, 1)]
	[InlineData(null, 2)]
	public async Task GivenVendorOptionRequestFailingWhenGetCapabilitiesTwiceThenAsksAgainOnlyIfItMayPass(int? status, int expectedRequests)
	{
		// Arrange — client-error-attributes-or-values-not-supported is the printer's answer; a failure to connect, with
		// no IPP status, may pass (provided by InlineData above)
		FakeIppClient client = new FakeIppClient { AttributeListFailureToThrow = new PrintException("Refused.") { NativeErrorCode = status } };
		client.AddPrinter(IppPrinterTests.PrinterUri, new PrinterAttributesBuilder().Build());
		RecordingLogger logger = new RecordingLogger();
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).WithLogger(logger).Build();
		IppPrinter printer = new IppPrinter(context, IppPrinterTests.PrinterUri, new PrinterInfo { Id = "cups:Office", Name = "Office" }, null);

		// Act
		await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);
		await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(expectedRequests, client.AttributeListRequestCount);
		Assert.Equal(expectedRequests, logger.Entries.Count);
	}

	[Fact]
	public async Task GivenPasswordRefusalFromPrinterListingIppsWhenSubmitThenSaysItsEncryptedAddress()
	{
		// Arrange — refused at Validate-Job for want of a password, by a printer listing an ipps:// address too
		FakeIppClient client = new FakeIppClient
		{
			ValidateJobFailureToThrow = new PrintAuthenticationException("Needs a user name and password.") { NativeErrorCode = 401 }
		};

		IppPrinter printer = IppPrinterTests.CreateValidatingPrinter
		(
			client,
			[IppOperation.PrintJob, IppOperation.ValidateJob],
			requirements: new PrinterUriRequirements
			{
				Authentication    = "basic",
				SecureAlternative = new Uri("ipps://localhost:443/printers/Office")
			}
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — the same kind of exception and status, with the address added
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(submit);
		Assert.StartsWith("Needs a user name and password. ", exception.Message, StringComparison.Ordinal);
		Assert.Contains("ipps://localhost:443/printers/Office", exception.Message, StringComparison.Ordinal);
		Assert.Equal(401, exception.NativeErrorCode);
		Assert.Same(client.ValidateJobFailureToThrow, exception.InnerException);
	}

	[Fact]
	public async Task GivenRefusalFromPrinterAskingForDigestWhenSubmitThenSaysLatiprimereCantGiveIt()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient
		{
			PrintJobFailureToThrow = new PrintAuthenticationException("Needs a user name and password.") { NativeErrorCode = 401 }
		};

		IppPrinter printer = IppPrinterTests.CreateValidatingPrinter
		(
			client,
			[IppOperation.PrintJob],
			requirements: new PrinterUriRequirements { Authentication = "digest" }
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(submit);
		Assert.Contains("'digest' authentication", exception.Message, StringComparison.Ordinal);
		Assert.Contains("HTTP Basic only", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenPasswordRefusalWithNothingToAddWhenSubmitThenThrowsItUnchanged()
	{
		// Arrange — reached over IPPS already, so there is no other address to give
		PrintAuthenticationException refusal = new PrintAuthenticationException("Credentials rejected.") { NativeErrorCode = 401 };
		FakeIppClient client = new FakeIppClient { ValidateJobFailureToThrow = refusal };

		IppPrinter printer = IppPrinterTests.CreateValidatingPrinter
		(
			client,
			[IppOperation.PrintJob, IppOperation.ValidateJob],
			requirements: new PrinterUriRequirements { Authentication = "basic" }
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(submit);
		Assert.Same(refusal, exception);
	}

	[Fact]
	public async Task GivenPrinterThatRefusesAtValidateJobWhenSubmitThenThrowsItsStatusWithoutSendingTheDocument()
	{
		// Arrange — client-error-attributes-or-values-not-supported, before anything is uploaded (ADR 0033)
		FakeIppClient client = new FakeIppClient
		{
			ValidateJobFailureToThrow = new PrintException("Refused.") { NativeErrorCode = 0x040B }
		};

		IppPrinter printer = IppPrinterTests.CreateValidatingPrinter(client, [IppOperation.PrintJob, IppOperation.ValidateJob]);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintOptions options = new PrintOptions
		{
			JobName       = "Report",
			Duplex        = DuplexMode.LongEdge,
			VendorOptions = new Dictionary<string, string> { ["ipp:print-quality"] = "5" }
		};

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert — Validate-Job carried what Print-Job would have
		PrintException exception = await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Equal(0x040B, exception.NativeErrorCode);
		Assert.Null(client.LastPrintJobRequest);

		ValidateJobRequest validation = Assert.Single(client.ValidateJobRequests);
		Assert.Equal(IppPrinterTests.PrinterUri, validation.OperationAttributes?.PrinterUri?.Value);
		Assert.Equal("Report", validation.OperationAttributes?.JobName?.Value);
		Assert.Equal("application/pdf", validation.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Equal(Sides.TwoSidedLongEdge, validation.JobTemplateAttributes?.Sides?.Value);
		Assert.Contains(client.LastValidateJobVendorAttributes!.Job, attribute => attribute.Name == "print-quality");
	}

	[Fact]
	public async Task GivenPrinterThatAcceptsAtValidateJobWhenSubmitThenSendsTheJob()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		IppPrinter printer = IppPrinterTests.CreateValidatingPrinter(client, [IppOperation.PrintJob, IppOperation.ValidateJob]);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Single(client.ValidateJobRequests);
		Assert.NotNull(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenPrinterTooBusyToValidateWhenSubmitThenSendsTheJobAnywayAndLogsIt()
	{
		// Arrange — server-error-busy isn't a judgement of the job; Print-Job meets the same check
		FakeIppClient client = new FakeIppClient
		{
			ValidateJobFailureToThrow = new PrintException("Busy.") { NativeErrorCode = (int)IppStatusCode.ServerErrorBusy }
		};

		RecordingLogger logger = new RecordingLogger();
		IppPrinter printer = IppPrinterTests.CreateValidatingPrinter(client, [IppOperation.PrintJob, IppOperation.ValidateJob], logger);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(client.LastPrintJobRequest);
		Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Debug, Assert.Single(logger.Entries).Level);
	}

	[Theory]
	[InlineData(true, false, true)]
	[InlineData(false, true, true)]
	[InlineData(true, true, false)]
	public async Task GivenValidationOffOrNotListedWhenSubmitThenSendsNoValidateJob(bool validateJobs, bool listsValidateJob, bool operationsKnown)
	{
		// Arrange — validation off, a printer that doesn't list the operation, or one whose operations weren't read
		// (provided by InlineData above)
		FakeIppClient client = new FakeIppClient();
		IppOperation[]? operations = !operationsKnown ? null
			: listsValidateJob ? [IppOperation.PrintJob, IppOperation.ValidateJob]
			: [IppOperation.PrintJob];

		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).WithValidateJobs(validateJobs).Build();
		IppPrinter printer = new IppPrinter(context, IppPrinterTests.PrinterUri, new PrinterInfo { Id = "ipp:x", Name = "x" }, null, operations);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Empty(client.ValidateJobRequests);
		Assert.NotNull(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenPrinterThatHasGoneWhenGetCapabilitiesThenThrowsPrintException()
	{
		// Arrange — the fake client answers not-found for a URI with no printer
		IppPrinter printer = IppPrinterTests.CreatePrinter(new FakeIppClient());

		// Act
		Func<Task> getCapabilities = () => printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintException>(getCapabilities);
	}

	[Fact]
	public async Task GivenNullContentWhenSubmitThenThrowsArgumentNullException()
	{
		// Arrange
		IppPrinter printer = IppPrinterTests.CreatePrinter(new FakeIppClient());
		PrintContent? content = null;

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			content!, // Deliberately null to test the guard
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		await Assert.ThrowsAsync<ArgumentNullException>(submit);
	}

	[Fact]
	public async Task GivenPrinterBusyTwiceWhenSubmitThenResendsWholeDocumentUntilAccepted()
	{
		// Arrange — the document starts two bytes into the stream, and each attempt must send it from there
		FakeIppClient client = new FakeIppClient { BusyPrintJobCount = 2, PrintJobId = 7 };
		IppPrinter printer = IppPrinterTests.CreateRetryingPrinter(client, TimeSpan.FromMinutes(1));
		using MemoryStream document = new MemoryStream(new byte[] { 0xFF, 0xFF, 0x25, 0x50, 0x44, 0x46 });
		document.Position = 2;

		// Act
		IPrintJob job = await printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.Equal("7", job.Id);
		Assert.Equal(3, client.ReceivedDocuments.Count);
		Assert.All(client.ReceivedDocuments, received => Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, received));
	}

	[Fact]
	public async Task GivenNoBusyRetryWhenPrinterBusyThenThrowsAfterOneAttempt()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient { BusyPrintJobCount = 1 };
		IppPrinter printer = IppPrinterTests.CreateRetryingPrinter(client, TimeSpan.Zero);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Equal(IppPrinterTests.ServerErrorBusy, exception.NativeErrorCode);
		Assert.Single(client.ReceivedDocuments);
	}

	[Fact]
	public async Task GivenPrinterBusyThroughoutWhenRetryTimeRunsOutThenThrowsBusyError()
	{
		// Arrange — a clock that moves 10 ms each time it's read, so the retry time runs out after the same number of
		// attempts however slow the machine: the waits of 1, 2, 4 and 4 ms each still fit within 50 ms after the first
		// four attempts, and the wait after the fifth doesn't
		FakeIppClient client = new FakeIppClient { BusyPrintJobCount = int.MaxValue };
		SteppingTimeProvider clock = new SteppingTimeProvider(TimeSpan.FromMilliseconds(10));
		IppPrinter printer = IppPrinterTests.CreateRetryingPrinter(client, TimeSpan.FromMilliseconds(50), clock);
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Equal(IppPrinterTests.ServerErrorBusy, exception.NativeErrorCode);
		Assert.Equal(5, client.ReceivedDocuments.Count);
	}

	[Fact]
	public async Task GivenUnseekableDocumentWhenPrinterBusyThenThrowsWithoutResending()
	{
		// Arrange — a stream that can't go back can't be sent again
		FakeIppClient client = new FakeIppClient { BusyPrintJobCount = 1 };
		IppPrinter printer = IppPrinterTests.CreateRetryingPrinter(client, TimeSpan.FromMinutes(1));
		using ForwardOnlyStream document = new ForwardOnlyStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Equal(IppPrinterTests.ServerErrorBusy, exception.NativeErrorCode);
		Assert.Single(client.ReceivedDocuments);
	}

	[Fact]
	public async Task GivenRejectionOtherThanBusyWhenSubmitThenThrowsWithoutResending()
	{
		// Arrange
		PrintException rejection = new PrintException("Not supported.") { NativeErrorCode = 0x040B };
		FakeIppClient client = new FakeIppClient { FailureToThrow = rejection };
		IppPrinter printer = IppPrinterTests.CreateRetryingPrinter(client, TimeSpan.FromMinutes(1));
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert — the configured failure is thrown before the request is recorded, so nothing was sent twice
		PrintException exception = await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Same(rejection, exception);
		Assert.Empty(client.ReceivedDocuments);
	}

	[Fact]
	public async Task GivenJobOnThisQueueWhenGetJobThenAsksForItAndReturnsIt()
	{
		// Arrange — CUPS names the queue by its own host and port, which differ from the ones used to reach it
		FakeIppClient client = new FakeIppClient
		{
			JobAttributes = new JobDescriptionAttributes
			{
				JobState      = new IppValue<JobState>(JobState.Processing),
				JobPrinterUri = new IppValue<Uri>(new Uri("ipp://printserver.local:631/printers/Office"))
			}
		};

		IppPrinter printer = IppPrinterTests.CreatePrinter(client);

		// Act
		IPrintJob? job = await printer.GetJobAsync("42", TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(job);
		Assert.Equal("42", job.Id);
		Assert.Equal("cups:Office", job.PrinterId);
		GetJobAttributesRequest request = Assert.IsType<GetJobAttributesRequest>(client.LastGetJobAttributesRequest);
		Assert.Equal(42, request.OperationAttributes?.JobId?.Value);
		Assert.Equal(IppPrinterTests.PrinterUri, request.OperationAttributes?.PrinterUri?.Value);
		Assert.Equal("tester", request.OperationAttributes?.RequestingUserName?.Value);
	}

	[Fact]
	public async Task GivenJobFoundWhenGetJobThenTheJobReportsItsStatus()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient
		{
			JobAttributes = new JobDescriptionAttributes { JobState = new IppValue<JobState>(JobState.Pending) }
		};

		IppPrinter printer = IppPrinterTests.CreatePrinter(client);
		IPrintJob? job = await printer.GetJobAsync("7", TestContext.Current.CancellationToken);
		Assert.NotNull(job);

		// Act
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Pending, status.State);
		Assert.Equal("7", status.JobId);
	}

	[Fact]
	public async Task GivenJobThePrinterNoLongerKnowsWhenGetJobThenReturnsNull()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient
		{
			JobFailureToThrow = new PrintException("Not found.") { NativeErrorCode = (int)IppStatusCode.ClientErrorNotFound }
		};

		IppPrinter printer = IppPrinterTests.CreatePrinter(client);

		// Act
		IPrintJob? job = await printer.GetJobAsync("42", TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(job);
	}

	[Fact]
	public async Task GivenJobOnAnotherCupsQueueWhenGetJobThenReturnsNull()
	{
		// Arrange — CUPS numbers jobs across its queues, so another queue's job answers too
		FakeIppClient client = new FakeIppClient
		{
			JobAttributes = new JobDescriptionAttributes
			{
				JobState      = new IppValue<JobState>(JobState.Pending),
				JobPrinterUri = new IppValue<Uri>(new Uri("ipp://localhost:631/printers/Warehouse"))
			}
		};

		IppPrinter printer = IppPrinterTests.CreatePrinter(client);

		// Act
		IPrintJob? job = await printer.GetJobAsync("42", TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(job);
	}

	[Fact]
	public async Task GivenPrinterFailsWhenGetJobThenThrowsItsPrintException()
	{
		// Arrange
		PrintException failure = new PrintException("Internal error.") { NativeErrorCode = (int)IppStatusCode.ServerErrorInternalError };
		FakeIppClient client = new FakeIppClient { JobFailureToThrow = failure };
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);

		// Act
		Func<Task> getJob = () => printer.GetJobAsync("42", TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(getJob);
		Assert.Same(failure, exception);
	}

	[Theory]
	[InlineData("")]
	[InlineData("abc")]
	[InlineData("0")]
	[InlineData("-1")]
	[InlineData(" 42")]
	[InlineData("ipp:42")]
	[InlineData("99999999999")]
	public async Task GivenIdThePrinterCouldNotHaveGivenWhenGetJobThenReturnsNullWithoutAsking(string jobId)
	{
		// Arrange — jobId is not a positive job-id (provided by InlineData above)
		FakeIppClient client = new FakeIppClient();
		IppPrinter printer = IppPrinterTests.CreatePrinter(client);

		// Act
		IPrintJob? job = await printer.GetJobAsync(jobId, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(job);
		Assert.Null(client.LastGetJobAttributesRequest);
	}

	[Fact]
	public async Task GivenJobIdPrefixWhenSubmitThenGetJobThenTheIdsCarryItAndFindTheJob()
	{
		// Arrange — as for a Windows queue's printer, whose spooler job IDs are numbers too
		FakeIppClient client = new FakeIppClient
		{
			PrintJobId    = 42,
			JobAttributes = new JobDescriptionAttributes { JobState = new IppValue<JobState>(JobState.Pending) }
		};

		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).Build();
		IppPrinter printer = new IppPrinter(context, IppPrinterTests.PrinterUri, new PrinterInfo { Id = "windows:Office", Name = "Office" })
		{
			JobIdPrefix = "ipp:"
		};

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
		IPrintJob submitted = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Act
		IPrintJob? found = await printer.GetJobAsync(submitted.Id, TestContext.Current.CancellationToken);
		IPrintJob? unprefixed = await printer.GetJobAsync("42", TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("ipp:42", submitted.Id);
		Assert.NotNull(found);
		Assert.Equal("ipp:42", found.Id);
		Assert.Equal(42, client.LastGetJobAttributesRequest?.OperationAttributes?.JobId?.Value);
		Assert.Null(unprefixed);
	}

	/// <summary>
	/// Creates a printer that resends jobs turned away as busy, with millisecond waits between attempts.
	/// </summary>
	/// <param name="client">The fake IPP client.</param>
	/// <param name="busyRetryDuration">How long to keep resending.</param>
	/// <param name="timeProvider">The clock the retries are timed by, or <see langword="null"/> for the system's.</param>
	/// <returns>The printer.</returns>
	private static IppPrinter CreateRetryingPrinter(FakeIppClient client, TimeSpan busyRetryDuration, TimeProvider? timeProvider = null)
	{
		IppPrintContext context = new IppPrintContextBuilder()
			.WithClient(client)
			.WithBusyRetryDuration(busyRetryDuration)
			.WithTimeProvider(timeProvider ?? TimeProvider.System)
			.Build();
		PrinterInfo info = new PrinterInfo { Id = "ipp:ipp://printer.local/ipp/print", Name = "Office" };

		return new IppPrinter(context, IppPrinterTests.PrinterUri, info);
	}

	/// <summary>
	/// Creates a direct IPP printer that validates jobs, listing the given operations.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="operations">The operations the printer lists.</param>
	/// <param name="logger">Where to log, or nowhere.</param>
	/// <param name="requirements">What the printer says it asks for at its address, or <see langword="null"/>.</param>
	/// <returns>The printer.</returns>
	private static IppPrinter CreateValidatingPrinter
	(
		FakeIppClient                    client,
		IppOperation[]                   operations,
		RecordingLogger?                 logger = null,
		PrinterUriRequirements?  requirements = null
	)
	{
		IppPrintContextBuilder builder = new IppPrintContextBuilder().WithClient(client).WithValidateJobs(true);
		IppPrintContext context = (logger is null ? builder : builder.WithLogger(logger)).Build();
		PrinterInfo info = new PrinterInfo { Id = "ipp:ipp://printer/ipp/print", Name = "Office" };

		return new IppPrinter(context, IppPrinterTests.PrinterUri, info, null, operations)
		{
			UriRequirements = requirements
		};
	}

	/// <summary>
	/// Creates a printer that uses the fake client and CUPS document formats.
	/// </summary>
	/// <param name="client">The fake IPP client.</param>
	/// <param name="supportedFormats">
	/// The MIME types the printer accepts, or <see langword="null"/> to skip the check.
	/// </param>
	/// <returns>The printer.</returns>
	private static IppPrinter CreatePrinter
	(
		FakeIppClient                client,
		IReadOnlyCollection<string>? supportedFormats = null
	)
	{
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).Build();
		PrinterInfo info = new PrinterInfo { Id = "cups:Office", Name = "Office" };

		return new IppPrinter(context, IppPrinterTests.PrinterUri, info, supportedFormats);
	}
}
