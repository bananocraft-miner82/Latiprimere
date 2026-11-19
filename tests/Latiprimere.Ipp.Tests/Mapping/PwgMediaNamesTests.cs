using Latiprimere.Ipp.Mapping;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

public sealed class PwgMediaNamesTests
{
	[Theory]
	[InlineData("iso_a4_210x297mm", "A4", 210.0, 297.0)]
	[InlineData("na_letter_8.5x11in", "Letter", 215.9, 279.4)]
	[InlineData("na_number-10_4.125x9.5in", "Number 10", 104.78, 241.3)]
	[InlineData("jis_b5_182x257mm", "B5 (JIS)", 182.0, 257.0)]
	[InlineData("iso_dl_110x220mm", "DL", 110.0, 220.0)]
	[InlineData("na_super-b_13x19in", "Super B", 330.2, 482.6)]
	[InlineData("na_index-4x6_4x6in", "Index 4x6", 101.6, 152.4)]
	[InlineData("custom_148.52x209.9mm_148.52x209.9mm", "148.52 × 209.9 mm", 148.52, 209.9)]
	[InlineData("custom_16x20in_16x20in", "16 × 20 in", 406.4, 508.0)]
	public void GivenSelfDescribingNameWhenReadThenGivesSizeInMillimetresAndDisplayName
	(
		string name,
		string expectedDisplayName,
		double expectedWidth,
		double expectedHeight
	)
	{
		// Arrange — standard and custom names in both units (provided by InlineData above)

		// Act
		MediaSize? size = PwgMediaNames.ToMediaSize(name);

		// Assert
		Assert.NotNull(size);
		Assert.Equal(name, size.Name);
		Assert.Equal(expectedDisplayName, size.DisplayName);
		Assert.Equal(expectedWidth, size.WidthMillimeters, 2);
		Assert.Equal(expectedHeight, size.HeightMillimeters, 2);
	}

	[Theory]
	[InlineData("auto")]
	[InlineData("iso_a4")]
	[InlineData("iso_a4_210x297cm")]
	[InlineData("custom_min_3x5in")]
	[InlineData("roll_max_36x1188in")]
	[InlineData("custom_x_99999999999999999999999999999999x1mm")]
	[InlineData("custom_x_1x1.99999999999999999999999999999mm")]
	public void GivenNameThatIsNotASizeWhenReadThenReturnsNull(string name)
	{
		// Arrange — keywords without dimensions, an unknown unit, and custom/roll limits (provided by InlineData)

		// Act
		MediaSize? size = PwgMediaNames.ToMediaSize(name);

		// Assert
		Assert.Null(size);
	}
}
