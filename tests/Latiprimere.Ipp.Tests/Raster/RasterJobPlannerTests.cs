using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Raster;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Raster;

public sealed class RasterJobPlannerTests
{
	private static readonly MediaSize A5 = new MediaSize { Name = "iso_a5_148x210mm", DisplayName = "A5", WidthMillimeters = 148, HeightMillimeters = 210 };

	[Theory]
	[InlineData(new[] { "application/octet-stream", "image/urf", "image/pwg-raster" }, (int)RasterFormat.PwgRaster)]
	[InlineData(new[] { "application/vnd.hp-PCL", "IMAGE/URF" }, (int)RasterFormat.AppleRaster)]
	public void GivenRasterFormatsWhenChoosingThenPrefersPwgRaster(string[] formats, int expected)
	{
		// Arrange (provided by InlineData above)

		// Act
		RasterFormat? format = RasterJobPlanner.ChooseFormat(formats);

		// Assert
		Assert.Equal((RasterFormat)expected, format);
	}

	[Fact]
	public void GivenNoRasterFormatWhenChoosingThenNone()
	{
		// Arrange
		string[] formats = ["application/octet-stream", "image/jpeg"];

		// Act
		RasterFormat? format = RasterJobPlanner.ChooseFormat(formats);

		// Assert
		Assert.Null(format);
	}

	[Fact]
	public void GivenTheHpsFormatsWhenChoosingInTheDefaultOrderThenAppleRasterBeforePclm()
	{
		// Arrange — the HP takes both, and PCLm comes last by default
		string[] formats = ["application/vnd.hp-PCL", "image/jpeg", "application/PCLm", "image/urf", "application/octet-stream"];

		// Act
		RasterFormat? format = RasterJobPlanner.ChooseFormat(formats);

		// Assert
		Assert.Equal(RasterFormat.AppleRaster, format);
	}

	[Theory]
	[InlineData(new[] { (int)RasterFormat.Pclm, (int)RasterFormat.AppleRaster }, (int)RasterFormat.Pclm)]
	[InlineData(new[] { (int)RasterFormat.PwgRaster, (int)RasterFormat.Pclm }, (int)RasterFormat.Pclm)]
	[InlineData(new[] { (int)RasterFormat.AppleRaster }, (int)RasterFormat.AppleRaster)]
	public void GivenAnOrderWhenChoosingThenTheFirstThePrinterTakes(int[] order, int expected)
	{
		// Arrange — the HP's raster formats, in the order an application set (provided by InlineData above)
		string[] formats = ["image/jpeg", "APPLICATION/PCLM", "image/urf"];

		// Act
		RasterFormat? format = RasterJobPlanner.ChooseFormat(formats, order.Select(value => (RasterFormat)value).ToArray());

		// Assert
		Assert.Equal((RasterFormat)expected, format);
	}

	[Theory]
	[InlineData(new int[0])]
	[InlineData(new[] { (int)RasterFormat.PwgRaster })]
	public void GivenAnOrderThePrinterTakesNoneOfWhenChoosingThenNone(int[] order)
	{
		// Arrange — nothing allowed, or only a format the HP doesn't take (provided by InlineData above)
		string[] formats = ["application/PCLm", "image/urf"];

		// Act
		RasterFormat? format = RasterJobPlanner.ChooseFormat(formats, order.Select(value => (RasterFormat)value).ToArray());

		// Assert
		Assert.Null(format);
	}

	[Fact]
	public void GivenTheHpsAttributesWhenPlannedAsPclmThenColourAt600DpiInRunLengthStripsOf32WithRotatedBackSides()
	{
		// Arrange — rle is the first it prefers that Latiprimere writes
		IReadOnlyList<IppAttribute> attributes = RasterJobPlannerTests.HpAttributes();

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.Pclm, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		Assert.Equal("application/PCLm", plan.MimeType);
		Assert.True(plan.Gzip);
		Assert.Equal(RasterFormat.Pclm, plan.Settings.Format);
		Assert.Equal(RasterColorSpace.Srgb8, plan.Settings.ColorSpace);
		Assert.Equal(600, plan.Settings.Dpi);
		Assert.Equal(32, plan.Settings.PclmStripHeight);
		Assert.Equal(PdfStreamEncoding.RunLength, plan.Settings.PclmCompression);
		Assert.Equal(RasterBackSide.Rotated, plan.Settings.BackSide);
	}

	[Fact]
	public void GivenAPclmPrinterWhenPlannedInMonochromeWithALimitThenGreyAtTheLimit()
	{
		// Arrange
		PrintOptions options = new PrintOptions { ColorMode = ColorMode.Monochrome };

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.Pclm, RasterJobPlannerTests.HpAttributes(), options, maxRenderDpi: 300);

		// Assert
		Assert.Equal(RasterColorSpace.Gray8, plan.Settings.ColorSpace);
		Assert.Equal(300, plan.Settings.Dpi);
	}

	[Theory]
	[InlineData(null, null, new[] { "jpeg" }, 16, (int)PdfStreamEncoding.Flate)]
	[InlineData(null, new[] { 128, 64 }, new[] { "flate", "rle" }, 64, (int)PdfStreamEncoding.Flate)]
	[InlineData(100000, null, null, 16, (int)PdfStreamEncoding.Flate)]
	[InlineData(8, new[] { 8, 16 }, new[] { "jpeg", "rle" }, 8, (int)PdfStreamEncoding.RunLength)]
	public void GivenPclmStripAttributesWhenPlannedThenChoosesTheStripHeightAndCompression
	(
		int? preferred,
		int[]? supported,
		string[]? compression,
		int expectedHeight,
		int expectedCompression
	)
	{
		// Arrange — nothing preferred and only JPEG, which Latiprimere doesn't write; the lowest supported; a preferred
		// height too large to trust; a small preferred height, and rle after jpeg (provided by InlineData above)
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Resolution, "pclm-source-resolution-supported", new Resolution(300, 300, ResolutionUnit.DotsPerInch)),
			new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm"),
		];

		if (preferred is not null)
		{
			attributes.Add(new IppAttribute(Tag.Integer, "pclm-strip-height-preferred", preferred.Value));
		}

		attributes.AddRange((supported ?? []).Select((height, index) => new IppAttribute(Tag.Integer, index == 0 ? "pclm-strip-height-supported" : string.Empty, height)));
		attributes.AddRange((compression ?? []).Select((method, index) => new IppAttribute(Tag.Keyword, index == 0 ? "pclm-compression-method-preferred" : string.Empty, method)));

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.Pclm, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		Assert.Equal(expectedHeight, plan.Settings.PclmStripHeight);
		Assert.Equal((PdfStreamEncoding)expectedCompression, plan.Settings.PclmCompression);
		Assert.Equal(300, plan.Settings.Dpi);
		Assert.Equal(RasterBackSide.Normal, plan.Settings.BackSide);
	}

	[Fact]
	public void GivenAPclmPrinterListingNoResolutionWhenPlannedThenNotSupported()
	{
		// Arrange — only an Apple raster resolution, which PCLm doesn't use
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Keyword, "urf-supported", "RS300"),
			new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm"),
		];

		// Act
		Action plan = () => RasterJobPlanner.Plan(RasterFormat.Pclm, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(plan);
		Assert.Contains("application/PCLm", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenTheHpsAttributesWhenPlannedThenAppleRasterInColourAt600DpiGzippedWithRotatedBackSides()
	{
		// Arrange — what the HP ENVY 5640 reports
		IReadOnlyList<IppAttribute> attributes = RasterJobPlannerTests.HpAttributes();

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.AppleRaster, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		Assert.Equal("image/urf", plan.MimeType);
		Assert.True(plan.Gzip);
		Assert.Equal(RasterFormat.AppleRaster, plan.Settings.Format);
		Assert.Equal(RasterColorSpace.Srgb8, plan.Settings.ColorSpace);
		Assert.Equal(600, plan.Settings.Dpi);
		Assert.Equal("iso_a4_210x297mm", plan.Settings.Media.Name);
		Assert.Equal(DuplexMode.OneSided, plan.Settings.Duplex);
		Assert.Equal(RasterBackSide.Rotated, plan.Settings.BackSide);
	}

	[Fact]
	public void GivenOptionsWhenPlannedThenTheyChooseColourPaperSidesAndOrientation()
	{
		// Arrange
		PrintOptions options = new PrintOptions
		{
			ColorMode   = ColorMode.Monochrome,
			MediaSize   = RasterJobPlannerTests.A5,
			Duplex      = DuplexMode.ShortEdge,
			Orientation = PageOrientation.Landscape
		};

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.AppleRaster, RasterJobPlannerTests.HpAttributes(), options, maxRenderDpi: 300);

		// Assert
		Assert.Equal(RasterColorSpace.Gray8, plan.Settings.ColorSpace);
		Assert.Equal(300, plan.Settings.Dpi);
		Assert.Same(RasterJobPlannerTests.A5, plan.Settings.Media);
		Assert.Equal(DuplexMode.ShortEdge, plan.Settings.Duplex);
		Assert.Equal(PageOrientation.Landscape, plan.Settings.Orientation);
	}

	[Fact]
	public void GivenIppeveprintersAttributesWhenPlannedThenPwgRasterInGreyAtItsDefault()
	{
		// Arrange — ippeveprinter's PWG raster printer: black_1 and sgray_8, at 300 or 600 dpi
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Resolution, "pwg-raster-document-resolution-supported", new Resolution(300, 300, ResolutionUnit.DotsPerInch)),
			new IppAttribute(Tag.Resolution, string.Empty, new Resolution(600, 600, ResolutionUnit.DotsPerInch)),
			new IppAttribute(Tag.Keyword, "pwg-raster-document-type-supported", "black_1"),
			new IppAttribute(Tag.Keyword, string.Empty, "sgray_8"),
			new IppAttribute(Tag.Keyword, "pwg-raster-document-sheet-back", "normal"),
			new IppAttribute(Tag.Resolution, "printer-resolution-default", new Resolution(300, 300, ResolutionUnit.DotsPerInch)),
			new IppAttribute(Tag.Keyword, "media-default", "na_letter_8.5x11in"),
			new IppAttribute(Tag.Keyword, "sides-default", "two-sided-long-edge"),
			new IppAttribute(Tag.Keyword, "compression-supported", "none"),
		];

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, new PrintOptions { ColorMode = ColorMode.Color }, maxRenderDpi: null);

		// Assert — colour asked for but not taken: grey
		Assert.Equal("image/pwg-raster", plan.MimeType);
		Assert.False(plan.Gzip);
		Assert.Equal(RasterColorSpace.Gray8, plan.Settings.ColorSpace);
		Assert.Equal(300, plan.Settings.Dpi);
		Assert.Equal("na_letter_8.5x11in", plan.Settings.Media.Name);
		Assert.Equal(DuplexMode.LongEdge, plan.Settings.Duplex);
		Assert.Equal(RasterBackSide.Normal, plan.Settings.BackSide);
	}

	[Theory]
	[InlineData("flipped", (int)RasterBackSide.Flipped)]
	[InlineData("rotated", (int)RasterBackSide.Rotated)]
	[InlineData("manual-tumble", (int)RasterBackSide.ManualTumble)]
	public void GivenPwgSheetBackWhenPlannedThenBackSidesFollowIt(string sheetBack, int expected)
	{
		// Arrange (provided by InlineData above)
		List<IppAttribute> attributes = RasterJobPlannerTests.PwgAttributes("sgray_8");
		attributes.Add(new IppAttribute(Tag.Keyword, "pwg-raster-document-sheet-back", sheetBack));

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		Assert.Equal((RasterBackSide)expected, plan.Settings.BackSide);
	}

	[Theory]
	[InlineData("process-bi-level", (int)RasterColorSpace.Black1)]
	[InlineData("monochrome", (int)RasterColorSpace.Gray8)]
	[InlineData("color", (int)RasterColorSpace.Srgb8)]
	public void GivenThePrintersDefaultColourModeWhenPlannedThenItChoosesTheColourSpace(string colorModeDefault, int expected)
	{
		// Arrange (provided by InlineData above)
		List<IppAttribute> attributes = RasterJobPlannerTests.PwgAttributes("black_1", "sgray_8", "srgb_8");
		attributes.Add(new IppAttribute(Tag.Keyword, "print-color-mode-default", colorModeDefault));

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		Assert.Equal((RasterColorSpace)expected, plan.Settings.ColorSpace);
	}

	[Fact]
	public void GivenOnlyBlackWhenPlannedInGreyThenBlack()
	{
		// Arrange
		List<IppAttribute> attributes = RasterJobPlannerTests.PwgAttributes("black_1");

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, new PrintOptions { ColorMode = ColorMode.Monochrome }, maxRenderDpi: null);

		// Assert
		Assert.Equal(RasterColorSpace.Black1, plan.Settings.ColorSpace);
	}

	[Theory]
	[InlineData(new[] { 300, 600 }, 0, 0, 600)]
	[InlineData(new[] { 300, 600, 1200 }, 0, 0, 600)]
	[InlineData(new[] { 300, 600, 1200 }, 1200, 0, 1200)]
	[InlineData(new[] { 300, 600, 1200 }, 1200, 600, 600)]
	[InlineData(new[] { 300, 600 }, 0, 400, 300)]
	[InlineData(new[] { 600, 1200 }, 0, 300, 600)]
	[InlineData(new[] { 1200 }, 0, 0, 1200)]
	public void GivenResolutionsWhenChoosingThenTheDefaultWithinTheLimitOrTheHighestTo600(int[] supported, int printerDefault, int limit, int expected)
	{
		// Arrange — 0 means none (provided by InlineData above)

		// Act
		int dpi = RasterJobPlanner.ChooseDpi(supported, printerDefault == 0 ? null : printerDefault, limit == 0 ? null : limit);

		// Assert
		Assert.Equal(expected, dpi);
	}

	[Fact]
	public void GivenResolutionsInDotsPerCentimetreWhenPlannedThenConvertsThem()
	{
		// Arrange — 118 dpcm is 300 dpi
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Resolution, "pwg-raster-document-resolution-supported", new Resolution(118, 118, ResolutionUnit.DotsPerCm)),
			new IppAttribute(Tag.Resolution, string.Empty, new Resolution(300, 600, ResolutionUnit.DotsPerInch)),
			new IppAttribute(Tag.Keyword, "pwg-raster-document-type-supported", "sgray_8"),
			new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm"),
		];

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert — the non-square resolution is left out
		Assert.Equal(300, plan.Settings.Dpi);
	}

	[Fact]
	public void GivenNoResolutionWhenPlannedThenNotSupported()
	{
		// Arrange
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Keyword, "urf-supported", "W8"),
			new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm"),
		];

		// Act
		Action plan = () => RasterJobPlanner.Plan(RasterFormat.AppleRaster, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(plan);
		Assert.Contains("image/urf", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenNoColourSpaceWhenPlannedThenNotSupported()
	{
		// Arrange — Adobe RGB only
		List<IppAttribute> attributes = RasterJobPlannerTests.PwgAttributes("adobe-rgb_8");

		// Act
		Action plan = () => RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(plan);
		Assert.Contains("image/pwg-raster", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenADefaultPaperWithoutASizeWhenPlannedThenNotSupportedUnlessOneIsChosen()
	{
		// Arrange
		List<IppAttribute> attributes = RasterJobPlannerTests.PwgAttributes("sgray_8");
		attributes.RemoveAll(attribute => attribute.Name == "media-default");
		attributes.Add(new IppAttribute(Tag.Keyword, "media-default", "roll_current"));

		// Act
		Action plan = () => RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, PrintOptions.Default, maxRenderDpi: null);
		RasterJobPlan chosen = RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, new PrintOptions { MediaSize = RasterJobPlannerTests.A5 }, maxRenderDpi: null);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(plan);
		Assert.Contains("roll_current", exception.Message, StringComparison.Ordinal);
		Assert.Same(RasterJobPlannerTests.A5, chosen.Settings.Media);
	}

	[Fact]
	public void GivenAPrinterDefaultAbove2400DpiWhenPlannedThenItIsLeftOut()
	{
		// Arrange — a printer, hostile or broken, whose default and highest resolution is absurd
		List<IppAttribute> attributes = RasterJobPlannerTests.PwgAttributes("sgray_8");
		attributes.Add(new IppAttribute(Tag.Resolution, string.Empty, new Resolution(100_000, 100_000, ResolutionUnit.DotsPerInch)));
		attributes.Add(new IppAttribute(Tag.Resolution, "printer-resolution-default", new Resolution(100_000, 100_000, ResolutionUnit.DotsPerInch)));

		// Act
		RasterJobPlan plan = RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		Assert.Equal(300, plan.Settings.Dpi);
	}

	[Theory]
	[InlineData((int)RasterFormat.PwgRaster)]
	[InlineData((int)RasterFormat.AppleRaster)]
	public void GivenOnlyResolutionsAbove2400DpiWhenPlannedThenNotSupported(int formatValue)
	{
		// Arrange — in dots per centimetre, one that would overflow an int as dots per inch
		RasterFormat format = (RasterFormat)formatValue;
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Resolution, "pwg-raster-document-resolution-supported", new Resolution(int.MaxValue, int.MaxValue, ResolutionUnit.DotsPerCm)),
			new IppAttribute(Tag.Resolution, string.Empty, new Resolution(4800, 4800, ResolutionUnit.DotsPerInch)),
			new IppAttribute(Tag.Keyword, "pwg-raster-document-type-supported", "sgray_8"),
			new IppAttribute(Tag.Keyword, "urf-supported", "W8"),
			new IppAttribute(Tag.Keyword, string.Empty, "RS4800-2147483647"),
			new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm"),
		];

		// Act
		Action plan = () => RasterJobPlanner.Plan(format, attributes, PrintOptions.Default, maxRenderDpi: null);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(plan);
		Assert.Contains("2400", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("custom_huge_999999x999999mm", null)]
	[InlineData("iso_a4_210x297mm", 1e300)]
	public void GivenAPageTooLargeToRenderWhenPlannedThenNotSupported(string mediaDefault, double? chosenMillimetres)
	{
		// Arrange — the printer's default paper, or the application's own (provided by InlineData above)
		List<IppAttribute> attributes = RasterJobPlannerTests.PwgAttributes("sgray_8");
		attributes.RemoveAll(attribute => attribute.Name == "media-default");
		attributes.Add(new IppAttribute(Tag.Keyword, "media-default", mediaDefault));

		PrintOptions options = chosenMillimetres is null
			? PrintOptions.Default
			: new PrintOptions
			{
				MediaSize = new MediaSize { Name = "huge", DisplayName = "Huge", WidthMillimeters = chosenMillimetres.Value, HeightMillimeters = chosenMillimetres.Value }
			};

		// Act
		Action plan = () => RasterJobPlanner.Plan(RasterFormat.PwgRaster, attributes, options, maxRenderDpi: null);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(plan);
		Assert.Contains("100000", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// The HP ENVY 5640's raster attributes, as it reports them (Data/hp-envy-5640-printer-attributes.ipp).
	/// </summary>
	/// <returns>The attributes.</returns>
	internal static IReadOnlyList<IppAttribute> HpAttributes()
	{
		string[] urf = ["CP1", "MT1-2-8-9-10-11", "PQ3-4-5", "RS300-600", "SRGB24", "OB9", "OFU0", "W8-16", "DEVW8-16", "DEVRGB24-48", "ADOBERGB24-48", "DM3", "IS1-7", "V1.4"];

		List<IppAttribute> attributes = urf
			.Select((keyword, index) => new IppAttribute(Tag.Keyword, index == 0 ? "urf-supported" : string.Empty, keyword))
			.ToList();

		// Its paper set to A4, as in Europe; it was captured on US Letter
		attributes.Add(new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm"));
		attributes.Add(new IppAttribute(Tag.Keyword, "sides-default", "one-sided"));
		attributes.Add(new IppAttribute(Tag.Keyword, "print-color-mode-default", "auto"));
		attributes.Add(new IppAttribute(Tag.Resolution, "printer-resolution-default", new Resolution(600, 600, ResolutionUnit.DotsPerInch)));
		attributes.Add(new IppAttribute(Tag.Keyword, "compression-supported", "none"));
		attributes.Add(new IppAttribute(Tag.Keyword, string.Empty, "gzip"));

		// Its PCLm attributes
		attributes.Add(new IppAttribute(Tag.Resolution, "pclm-source-resolution-supported", new Resolution(300, 300, ResolutionUnit.DotsPerInch)));
		attributes.Add(new IppAttribute(Tag.Resolution, string.Empty, new Resolution(600, 600, ResolutionUnit.DotsPerInch)));
		attributes.Add(new IppAttribute(Tag.Resolution, "pclm-source-resolution-default", new Resolution(600, 600, ResolutionUnit.DotsPerInch)));
		attributes.Add(new IppAttribute(Tag.Integer, "pclm-strip-height-preferred", 32));
		attributes.Add(new IppAttribute(Tag.Integer, "pclm-strip-height-supported", 32));
		attributes.Add(new IppAttribute(Tag.Keyword, "pclm-compression-method-preferred", "rle"));
		attributes.Add(new IppAttribute(Tag.Keyword, string.Empty, "jpeg"));
		attributes.Add(new IppAttribute(Tag.Keyword, string.Empty, "flate"));
		attributes.Add(new IppAttribute(Tag.Keyword, "pclm-raster-back-side", "rotated"));

		return attributes;
	}

	/// <summary>
	/// A PWG raster printer's attributes at 300 dpi on A4, taking the given types.
	/// </summary>
	/// <param name="types">Its <c>pwg-raster-document-type-supported</c> values.</param>
	/// <returns>The attributes.</returns>
	internal static List<IppAttribute> PwgAttributes(params string[] types)
	{
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Resolution, "pwg-raster-document-resolution-supported", new Resolution(300, 300, ResolutionUnit.DotsPerInch)),
			new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm"),
		];

		attributes.AddRange(types.Select((type, index) => new IppAttribute(Tag.Keyword, index == 0 ? "pwg-raster-document-type-supported" : string.Empty, type)));

		return attributes;
	}
}
