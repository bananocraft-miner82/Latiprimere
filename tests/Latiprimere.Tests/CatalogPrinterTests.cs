using System.Reflection;
using Latiprimere.Tests.Builders;
using Latiprimere.Tests.Fakes;
using Xunit;

namespace Latiprimere.Tests;

/// <summary>
/// The catalog's own printers and jobs, around the print systems' (ADR 0055).
/// </summary>
public sealed class CatalogPrinterTests
{
	[Fact]
	public async Task GivenPrinterFromTheCatalogWhenAskedForThePrintSystemsPrinterThenGetFeatureFindsItWhereACastWouldNot()
	{
		// Arrange
		FakePrinter adapterPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("office").Build());
		PrinterCatalog catalog = CatalogPrinterTests.CreateCatalog(adapterPrinter);

		// Act
		IPrinter printer = await catalog.GetPrinterAsync("office", TestContext.Current.CancellationToken);

		// Assert
		Assert.IsNotType<FakePrinter>(printer);
		Assert.Same(adapterPrinter, printer.GetFeature<FakePrinter>());
		Assert.Same(printer, printer.GetFeature<IPrinter>());
		Assert.Null(printer.GetFeature<IDisposable>());
		Assert.Same(adapterPrinter.Info, printer.Info);
	}

	[Fact]
	public async Task GivenDefaultPrinterFromTheCatalogWhenReadThenItIsTheCatalogsToo()
	{
		// Arrange
		FakePrinter adapterPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("office").AsDefault().Build());
		PrinterCatalog catalog = CatalogPrinterTests.CreateCatalog(adapterPrinter);

		// Act
		IPrinter? printer = await catalog.GetDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(printer);
		Assert.IsNotType<FakePrinter>(printer);
		Assert.Same(adapterPrinter, printer.GetFeature<FakePrinter>());
	}

	[Fact]
	public async Task GivenSettingThePrinterDoesNotApplyWhenSubmittedThenRefusedBeforeAnythingIsSent()
	{
		// Arrange: a print system written before Copies existed would say so this way
		FakePrinter adapterPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("office").Build())
		{
			AppliedOptions = PrintOptionKinds.JobName | PrintOptionKinds.Duplex
		};

		IPrinter printer = await CatalogPrinterTests.CreateCatalog(adapterPrinter).GetPrinterAsync("office", TestContext.Current.CancellationToken);
		PrintOptions options = new PrintOptions { Copies = 2, ColorMode = ColorMode.Monochrome, Duplex = DuplexMode.LongEdge };
		using MemoryStream document = new MemoryStream([0x25]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromRaw(document), options, TestContext.Current.CancellationToken);

		// Assert: named, with the printer, and nothing reaches the print system
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("Copies, ColorMode", exception.Message, StringComparison.Ordinal);
		Assert.Contains("'office'", exception.Message, StringComparison.Ordinal);
		Assert.DoesNotContain("Duplex", exception.Message, StringComparison.Ordinal);
		Assert.Equal(0, adapterPrinter.SubmitCount);
	}

	[Fact]
	public async Task GivenSettingThePrinterDoesNotApplyLeftAtItsDefaultWhenSubmittedThenTheJobIsSent()
	{
		// Arrange
		FakePrinter adapterPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("office").Build())
		{
			AppliedOptions = PrintOptionKinds.None
		};

		IPrinter printer = await CatalogPrinterTests.CreateCatalog(adapterPrinter).GetPrinterAsync("office", TestContext.Current.CancellationToken);
		using MemoryStream document = new MemoryStream([0x25]);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert: the job is the catalog's too, around the print system's
		Assert.Equal(1, adapterPrinter.SubmitCount);
		Assert.IsNotType<FakePrintJob>(job);
		Assert.NotNull(job.GetFeature<FakePrintJob>());
		Assert.Equal("office", job.PrinterId);
	}

	[Fact]
	public async Task GivenEverySettingChangedWhenSubmittedToAPrinterApplyingNoneThenEachIsNamed()
	{
		// Arrange
		FakePrinter adapterPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("office").Build())
		{
			AppliedOptions = PrintOptionKinds.None
		};

		IPrinter printer = await CatalogPrinterTests.CreateCatalog(adapterPrinter).GetPrinterAsync("office", TestContext.Current.CancellationToken);
		using MemoryStream document = new MemoryStream([0x25]);

		PrintOptions options = new PrintOptions
		{
			JobName       = "Invoice",
			Copies        = 2,
			Duplex        = DuplexMode.LongEdge,
			Orientation   = PageOrientation.Landscape,
			MediaSize     = new MediaSize { Name = "iso_a5_148x210mm", DisplayName = "A5", WidthMillimeters = 148, HeightMillimeters = 210 },
			ColorMode     = ColorMode.Monochrome,
			Scaling       = ScalingMode.Fit,
			InputTray     = new InputTray { Id = "tray-2", Name = "Tray 2" },
			VendorOptions = new Dictionary<string, string> { ["ipp:print-quality"] = "5" },
			OutputFile    = "out.pdf",
			PageRanges    = [new PageRange(2, 3)],
			Collate       = false,
			Quality       = PrintQuality.High,
			Finishings    = [Finishing.StapleTopLeft],
			OutputBin     = new OutputBin { Id = "face-down", Name = "Face down" },
			MediaType     = new MediaType { Id = "labels", Name = "Labels" }
		};

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromRaw(document), options, TestContext.Current.CancellationToken);

		// Assert: every setting is seen as set, so none can slip past
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);

		foreach (string setting in Enum.GetNames<PrintOptionKinds>().Where(name => name != nameof(PrintOptionKinds.None)))
		{
			Assert.Contains(setting, exception.Message, StringComparison.Ordinal);
		}
	}

	[Fact]
	public async Task GivenPrinterFromTheCatalogWhenItsDetailsAreReadAndItIsIdentifiedThenThePrintSystemsPrinterIsAsked()
	{
		// Arrange
		PrinterDetails details = new PrinterDetails { PrinterId = "office", WebPage = new Uri("http://office.local/") };
		FakePrinter adapterPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("office").Build()) { Details = details };
		IPrinter printer = await CatalogPrinterTests.CreateCatalog(adapterPrinter).GetPrinterAsync("office", TestContext.Current.CancellationToken);

		// Act
		PrinterDetails read = await printer.GetDetailsAsync(TestContext.Current.CancellationToken);
		await printer.IdentifyAsync("Here", TestContext.Current.CancellationToken);

		// Assert
		Assert.Same(details, read);
		Assert.Equal(["Here"], adapterPrinter.IdentifyMessages);
	}

	[Fact]
	public async Task GivenPrinterFromTheCatalogWhenATestPageIsPrintedThenItsJobIsTheCatalogsToo()
	{
		// Arrange
		FakePrinter adapterPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("office").Build());
		IPrinter printer = await CatalogPrinterTests.CreateCatalog(adapterPrinter).GetPrinterAsync("office", TestContext.Current.CancellationToken);

		// Act
		IPrintJob job = await printer.PrintTestPageAsync(new PrintOptions { Copies = 2 }, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(1, adapterPrinter.TestPageCount);
		Assert.IsNotType<FakePrintJob>(job);
		Assert.NotNull(job.GetFeature<FakePrintJob>());
	}

	[Fact]
	public async Task GivenSettingThePrinterDoesNotApplyWhenATestPageIsPrintedThenRefusedBeforeAnythingIsSent()
	{
		// Arrange: a test page is a job, held to the same check (ADR 0059)
		FakePrinter adapterPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("office").Build())
		{
			AppliedOptions = PrintOptionKinds.JobName
		};

		IPrinter printer = await CatalogPrinterTests.CreateCatalog(adapterPrinter).GetPrinterAsync("office", TestContext.Current.CancellationToken);

		// Act
		Func<Task> print = () => printer.PrintTestPageAsync(new PrintOptions { Duplex = DuplexMode.LongEdge }, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(print);
		Assert.Contains("Duplex", exception.Message, StringComparison.Ordinal);
		Assert.Equal(0, adapterPrinter.TestPageCount);
	}

	[Fact]
	public void GivenPrintOptionsWhenTheirSettingsAreListedThenEachHasAFlagOfItsName()
	{
		// Arrange: a setting added to PrintOptions without a flag could never be refused, nor applied
		string[] settings = typeof(PrintOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(property => property.CanWrite)
			.Select(property => property.Name)
			.Order(StringComparer.Ordinal)
			.ToArray();

		// Act
		string[] flags = Enum.GetNames<PrintOptionKinds>()
			.Where(name => name != nameof(PrintOptionKinds.None))
			.Order(StringComparer.Ordinal)
			.ToArray();

		// Assert
		Assert.Equal(settings, flags);
	}

	[Fact]
	public void GivenPrinterOfYourOwnWhenItsAppliedOptionsAreReadThenTheDefaultIsEverySettingOfThisVersion()
	{
		// Arrange: a class of an application's own that doesn't say
		IPrinter printer = new SequencePrinter();

		// Act
		PrintOptionKinds applied = printer.AppliedOptions;

		// Assert
		PrintOptionKinds every = Enum.GetValues<PrintOptionKinds>().Aggregate(PrintOptionKinds.None, (all, kind) => all | kind);
		Assert.Equal(every, applied);
		Assert.Same(printer, printer.GetFeature<SequencePrinter>());
	}

	/// <summary>
	/// Creates a catalog of one print system with one printer.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>The catalog.</returns>
	private static PrinterCatalog CreateCatalog(FakePrinter printer)
	{
		return new PrinterCatalog([new FakePrintSystem("Fake", isSupported: true, printer)]);
	}
}
