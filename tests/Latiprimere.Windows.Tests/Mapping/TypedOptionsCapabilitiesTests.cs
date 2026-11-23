using System.Runtime.Versioning;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Mapping;
using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Mapping;

/// <summary>
/// A driver's paper types and custom sizes (ADR 0056).
/// </summary>
public sealed class TypedOptionsCapabilitiesTests
{
	[Fact]
	public void GivenADriverTakingCustomSizesWhenToCapabilitiesThenListsItsRangeAndTypes()
	{
		// Arrange: DMPAPER_USER among its papers, and extents in tenths of a millimetre
		WindowsCapabilitiesRecord record = TypedOptionsCapabilitiesTests.Record(takesCustom: true);

		// Act
		PrinterCapabilities capabilities = WindowsCapabilitiesMapper.ToCapabilities(record);

		// Assert
		MediaSizeRange range = Assert.Single(capabilities.CustomMediaSizes);
		Assert.Equal((25.4, 216.0, 25.4, 3200.0), (range.MinWidthMillimeters, range.MaxWidthMillimeters, range.MinHeightMillimeters, range.MaxHeightMillimeters));
		Assert.Equal([("1", "Plain paper"), ("258", "Labels")], capabilities.MediaTypes.Select(type => (type.Id, type.Name)));
	}

	[Fact]
	public void GivenADriverGivingExtentsButNoCustomPaperWhenToCapabilitiesThenListsNoRange()
	{
		// Arrange
		WindowsCapabilitiesRecord record = TypedOptionsCapabilitiesTests.Record(takesCustom: false);

		// Act
		PrinterCapabilities capabilities = WindowsCapabilitiesMapper.ToCapabilities(record);

		// Assert
		Assert.Empty(capabilities.CustomMediaSizes);
	}

	[Theory]
	[InlineData("258", 258u)]
	[InlineData("3", null)]
	[InlineData("labels", null)]
	public void GivenATypeIdWhenFoundThenOnlyTheDriversOwnAreFound(string id, uint? expected)
	{
		// Arrange (provided by InlineData above)
		WindowsCapabilitiesRecord record = TypedOptionsCapabilitiesTests.Record(takesCustom: true);

		// Act
		uint? found = WindowsCapabilitiesMapper.FindMediaTypeId(record, id);

		// Assert
		Assert.Equal(expected, found);
	}

	[Theory]
	[InlineData(0x0DE40100, 256, 3556)]
	[InlineData(-1, null, null)]
	[InlineData(0, null, null)]
	[SupportedOSPlatform("windows")] // Skipped elsewhere
	public void GivenAPackedExtentWhenUnpackedThenTheWidthIsTheLowWordAndTheLengthTheHigh(int packed, int? width, int? length)
	{
		// Arrange (provided by InlineData above)
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The spooler reader is Windows only.");

		// Act
		(short Width, short Length)? extent = WindowsCapabilityReader.UnpackExtent(packed);

		// Assert
		Assert.Equal(width, extent?.Width);
		Assert.Equal(length, extent?.Length);
	}

	[Fact]
	[SupportedOSPlatform("windows")] // Skipped elsewhere
	public void GivenListsOfDifferentLengthsWhenTypesAreCombinedThenOnlyThoseBothHaveAreKept()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The spooler reader is Windows only.");
		uint[] ids = [1, 258, 259];
		string[] names = ["Plain paper", "Labels"];

		// Act
		List<WindowsMediaTypeRecord> types = WindowsCapabilityReader.CombineMediaTypes(ids, names);

		// Assert
		Assert.Equal([(1u, "Plain paper"), (258u, "Labels")], types.Select(type => (type.Id, type.Name)));
	}

	/// <summary>
	/// Makes a driver's capabilities, with or without DMPAPER_USER among its papers.
	/// </summary>
	/// <param name="takesCustom">Whether it lists DMPAPER_USER.</param>
	/// <returns>The capabilities.</returns>
	internal static WindowsCapabilitiesRecord Record(bool takesCustom)
	{
		List<WindowsPaperRecord> papers = [new WindowsPaperRecord { Id = 9, Name = "A4", WidthTenthsOfMillimetre = 2100, HeightTenthsOfMillimetre = 2970 }];

		if (takesCustom)
		{
			papers.Add(new WindowsPaperRecord { Id = DevModeConstants.PaperUser, Name = "Custom", WidthTenthsOfMillimetre = 2160, HeightTenthsOfMillimetre = 3556 });
		}

		return new WindowsCapabilitiesRecord
		{
			Papers     = papers,
			MediaTypes = [new WindowsMediaTypeRecord { Id = 1, Name = "Plain paper" }, new WindowsMediaTypeRecord { Id = 258, Name = "Labels" }],
			MinExtent  = (254, 254),
			MaxExtent  = (2160, 32000)
		};
	}
}
