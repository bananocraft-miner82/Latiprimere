using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Mapping;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;
using Xunit;
using IppRange = SharpIpp.Protocol.Models.Range;

namespace Latiprimere.Ipp.Tests.Mapping;

/// <summary>
/// The options added before 1.0, as IPP attributes and capabilities (ADR 0056).
/// </summary>
public sealed class IppTypedOptionsTests
{
	[Fact]
	public void GivenTheNewSettingsWhenToJobTemplateAttributesThenEachIsSentAsItsAttribute()
	{
		// Arrange
		PrintOptions options = new PrintOptions
		{
			PageRanges = [new PageRange(1, 3), new PageRange(7, 7)],
			Collate    = false,
			Quality    = PrintQuality.High,
			Finishings = [Finishing.StapleTopLeft, Finishing.PunchDualLeft, Finishing.Fold],
			OutputBin  = new OutputBin { Id = "face-down", Name = "Face down" }
		};

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert
		Assert.Equal([new IppRange(1, 3), new IppRange(7, 7)], Assert.NotNull(attributes.PageRanges).Value);
		Assert.Equal(MultipleDocumentHandling.SeparateDocumentsUncollatedCopies, attributes.MultipleDocumentHandling?.Value);
		Assert.Equal(SharpIpp.Protocol.Models.PrintQuality.High, attributes.PrintQuality?.Value);
		Assert.Equal([Finishings.StapleTopLeft, Finishings.PunchDualLeft, Finishings.Fold], attributes.Finishings?.Value);
		Assert.Equal("face-down", attributes.OutputBin?.Value.Value);
	}

	[Fact]
	public void GivenCollatedCopiesWhenToJobTemplateAttributesThenSendsCollatedCopies()
	{
		// Arrange
		PrintOptions options = new PrintOptions { Copies = 2, Collate = true };

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert
		Assert.Equal(MultipleDocumentHandling.SeparateDocumentsCollatedCopies, attributes.MultipleDocumentHandling?.Value);
	}

	[Fact]
	public void GivenAMediaTypeWhenToJobTemplateAttributesThenItGoesInMediaColWithTheSize()
	{
		// Arrange: media-type exists only inside media-col
		PrintOptions options = new PrintOptions
		{
			MediaSize = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimeters = 210, HeightMillimeters = 297 },
			MediaType = new MediaType { Id = "labels", Name = "Labels" }
		};

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert
		Assert.Null(attributes.Media);
		MediaCol mediaCol = Assert.NotNull(attributes.MediaCol).Value;
		Assert.Equal("labels", mediaCol.MediaType?.Value.Value);
		Assert.Equal(new IppRange(21000, 21000), mediaCol.MediaSize?.Value.XDimension?.Value);
		Assert.Null(mediaCol.MediaSource);
	}

	[Fact]
	public void GivenACustomSizeWhenToJobTemplateAttributesThenItGoesAsDimensionsNotAName()
	{
		// Arrange: a printer may not know the custom name, but every IPP Everywhere printer takes dimensions
		PrintOptions options = new PrintOptions { MediaSize = MediaSize.Custom(80, 120.5) };

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert
		Assert.Null(attributes.Media);
		MediaCol mediaCol = Assert.NotNull(attributes.MediaCol).Value;
		Assert.Equal(new IppRange(8000, 8000), mediaCol.MediaSize?.Value.XDimension?.Value);
		Assert.Equal(new IppRange(12050, 12050), mediaCol.MediaSize?.Value.YDimension?.Value);
	}

	[Fact]
	public void GivenTheTypedQualityAndTheVendorKeyForItWhenAttributesAreMadeThenTheKeyIsRefused()
	{
		// Arrange
		PrintOptions options = new PrintOptions
		{
			Quality       = PrintQuality.Draft,
			VendorOptions = new Dictionary<string, string> { ["ipp:print-quality"] = "5" }
		};

		PrintJobRequest request = new PrintJobRequest { JobTemplateAttributes = IppJobTemplateMapper.ToJobTemplateAttributes(options) };

		// Act
		Func<IppVendorAttributes> map = () => IppVendorAttributeMapper.ToAttributes(options.VendorOptions, IppVendorAttributeMapper.NetworkPrefixes, request);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(map);
		Assert.Contains("ipp:print-quality", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenTheVendorKeyForQualityAloneWhenAttributesAreMadeThenItIsSentAsBefore()
	{
		// Arrange
		PrintOptions options = new PrintOptions { VendorOptions = new Dictionary<string, string> { ["ipp:print-quality"] = "5" } };
		PrintJobRequest request = new PrintJobRequest { JobTemplateAttributes = IppJobTemplateMapper.ToJobTemplateAttributes(options) };

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options.VendorOptions, IppVendorAttributeMapper.NetworkPrefixes, request);

		// Assert
		Assert.Contains(attributes.Job, attribute => attribute.Name == "print-quality");
	}

	[Fact]
	public void GivenAPrintersAttributesWhenToCapabilitiesThenTheNewCapabilitiesAreRead()
	{
		// Arrange: a roll label printer that also takes custom sheets, with types, bins, qualities and finishings
		PrinterDescriptionAttributes attributes = new PrinterDescriptionAttributes
		{
			MediaSupported = new IppValue<Media[]>(
			[
				new Media("na_index-4x6_4x6in", true),
				new Media("custom_min_25.4x25.4mm", true),
				new Media("custom_max_104x355.6mm", true),
				new Media("roll_min_25.4x6.35mm", true),
				new Media("roll_max_104x3000mm", true),
			]),
			MediaTypeSupported    = new IppValue<SharpIpp.Protocol.Models.MediaType[]>([new SharpIpp.Protocol.Models.MediaType("labels"), new SharpIpp.Protocol.Models.MediaType("stationery")]),
			OutputBinSupported    = new IppValue<SharpIpp.Protocol.Models.OutputBin[]>([new SharpIpp.Protocol.Models.OutputBin("face-down", true)]),
			PrintQualitySupported = new IppValue<SharpIpp.Protocol.Models.PrintQuality[]>([SharpIpp.Protocol.Models.PrintQuality.High, SharpIpp.Protocol.Models.PrintQuality.Draft]),
			PrintQualityDefault   = new IppValue<SharpIpp.Protocol.Models.PrintQuality>(SharpIpp.Protocol.Models.PrintQuality.Draft),
			FinishingsSupported   = new IppValue<Finishings[]>([Finishings.None, Finishings.Staple, Finishings.Bind, Finishings.PunchDualTop]),
			PageRangesSupported   = new IppValue<bool>(true)
		};

		// Act
		PrinterCapabilities capabilities = IppCapabilitiesMapper.ToCapabilities(attributes);

		// Assert: the limits aren't sizes, but give one range each
		Assert.Single(capabilities.MediaSizes);
		Assert.Equal(2, capabilities.CustomMediaSizes.Count);
		MediaSizeRange custom = Assert.Single(capabilities.CustomMediaSizes, range => !range.IsRoll);
		Assert.Equal((25.4, 104.0, 25.4, 355.6), (custom.MinWidthMillimeters, custom.MaxWidthMillimeters, custom.MinHeightMillimeters, custom.MaxHeightMillimeters));
		MediaSizeRange roll = Assert.Single(capabilities.CustomMediaSizes, range => range.IsRoll);
		Assert.Equal((6.35, 3000.0), (roll.MinHeightMillimeters, roll.MaxHeightMillimeters));

		Assert.Equal(["labels", "stationery"], capabilities.MediaTypes.Select(type => type.Id));
		Assert.Equal("Labels", capabilities.MediaTypes[0].Name);
		Assert.Equal("face-down", Assert.Single(capabilities.OutputBins).Id);
		Assert.Equal([PrintQuality.Draft, PrintQuality.High], capabilities.PrintQualities);
		Assert.Equal(PrintQuality.Draft, capabilities.DefaultPrintQuality);
		Assert.Equal([Finishing.Staple, Finishing.PunchDualTop], capabilities.Finishings);
		Assert.True(capabilities.SupportsPageRanges);
	}

	[Fact]
	public void GivenOnlyAMinimumWhenSizeRangesAreReadThenThereIsNoRange()
	{
		// Arrange
		string[] names = ["custom_min_25.4x25.4mm", "iso_a4_210x297mm"];

		// Act
		List<MediaSizeRange> ranges = PwgMediaNames.ToSizeRanges(names);

		// Assert
		Assert.Empty(ranges);
	}

	[Fact]
	public void GivenRangesWhenPagesAreSelectedThenThoseInsideTheDocumentAreListedInOrder()
	{
		// Arrange: a five-page document, and a range running past its end
		PageRange[] ranges = [new PageRange(2, 3), new PageRange(5, 8)];

		// Act
		IReadOnlyList<int> pages = PageSelection.Select(ranges, 5);

		// Assert
		Assert.Equal([1, 2, 4], pages);
		Assert.Equal([0, 1, 2], PageSelection.Select([], 3));
	}

	[Fact]
	public void GivenRangesPastTheEndWhenPagesAreSelectedThenThrowsArgumentExceptionNamingThem()
	{
		// Arrange
		PageRange[] ranges = [new PageRange(4, 6)];

		// Act
		Func<IReadOnlyList<int>> select = () => PageSelection.Select(ranges, 3);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(select);
		Assert.Contains("4-6", exception.Message, StringComparison.Ordinal);
	}
}
