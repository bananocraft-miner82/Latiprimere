using System.Runtime.Versioning;
using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Spooler;

/// <summary>
/// Checks of how driver answers are combined. The methods tested call nothing in Windows, so these run on every
/// platform; the attribute only satisfies the analyzer, as the reader as a whole is Windows-only.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCapabilityReaderTests
{
	[Fact]
	public void GivenNameSlotsWhenSplitThenEndsEachNameAtItsNullOrItsSlot()
	{
		// Arrange — slots of four: one name ended by a null, one that fills its slot with no null, one empty, and a
		// partial slot at the end
		char[] characters = "A4\0\0Tray\0\0\0\0Le".ToCharArray();

		// Act
		string[] names = WindowsCapabilityReader.SplitNames(characters, 4);

		// Assert — a name that fills its slot has no terminating null, so the slot ends it
		Assert.Equal(new[] { "A4", "Tray", string.Empty }, names);
	}

	[Fact]
	public void GivenNoCharactersWhenSplitThenReturnsNoNames()
	{
		// Arrange — the driver doesn't list names
		char[] characters = [];

		// Act
		string[] names = WindowsCapabilityReader.SplitNames(characters, 24);

		// Assert
		Assert.Empty(names);
	}

	[Fact]
	public void GivenPaperListsOfDifferentLengthsWhenCombinedThenUsesOnlyEntriesAllThreeHave()
	{
		// Arrange — three identifiers and names, but sizes for only two papers
		ushort[] ids = [9, 1, 0x8001];
		string[] names = ["A4", "Letter", "Custom"];
		int[] sizes = [2100, 2970, 2159, 2794];

		// Act
		List<WindowsPaperRecord> papers = WindowsCapabilityReader.CombinePapers(ids, names, sizes);

		// Assert
		Assert.Equal(2, papers.Count);
		Assert.Equal(9, papers[0].Id);
		Assert.Equal("A4", papers[0].Name);
		Assert.Equal(2100, papers[0].WidthTenthsOfMillimetre);
		Assert.Equal(2970, papers[0].HeightTenthsOfMillimetre);
		Assert.Equal("Letter", papers[1].Name);
		Assert.Equal(2794, papers[1].HeightTenthsOfMillimetre);
	}

	[Fact]
	public void GivenDriverIdentifierAboveShortRangeWhenPapersCombinedThenKeepsItsBits()
	{
		// Arrange — DEVMODE holds paper identifiers as shorts, so a driver's own identifier above 32767 wraps
		ushort[] ids = [0x8001];
		string[] names = ["Custom"];
		int[] sizes = [1000, 1500];

		// Act
		List<WindowsPaperRecord> papers = WindowsCapabilityReader.CombinePapers(ids, names, sizes);

		// Assert — the same bits DEVMODE's dmPaperSize carries, so the paper can be chosen again
		WindowsPaperRecord paper = Assert.Single(papers);
		Assert.Equal(unchecked((short)0x8001), paper.Id);
	}

	[Fact]
	public void GivenBinListsOfDifferentLengthsWhenCombinedThenUsesOnlyEntriesBothHave()
	{
		// Arrange — three identifiers but two names
		ushort[] ids = [7, 1, 0x0101];
		string[] names = ["Automatically select", "Upper tray"];

		// Act
		List<WindowsBinRecord> bins = WindowsCapabilityReader.CombineBins(ids, names);

		// Assert
		Assert.Equal(2, bins.Count);
		Assert.Equal(7, bins[0].Id);
		Assert.Equal("Automatically select", bins[0].Name);
		Assert.Equal(1, bins[1].Id);
		Assert.Equal("Upper tray", bins[1].Name);
	}
}
