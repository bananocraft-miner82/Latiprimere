using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrinterCapabilitiesTests
{
	[Fact]
	public void GivenNewCapabilitiesWhenReadThenEverythingIsNotReported()
	{
		// Arrange
		PrinterCapabilities capabilities;

		// Act
		capabilities = new PrinterCapabilities();

		// Assert — empty lists and null defaults mean the print system didn't say
		Assert.Empty(capabilities.MediaSizes);
		Assert.Null(capabilities.DefaultMediaSize);
		Assert.Empty(capabilities.DuplexModes);
		Assert.Null(capabilities.DefaultDuplexMode);
		Assert.Empty(capabilities.ColorModes);
		Assert.Null(capabilities.DefaultColorMode);
		Assert.Null(capabilities.MaxCopies);
		Assert.Empty(capabilities.Orientations);
		Assert.Empty(capabilities.InputTrays);
	}

	[Fact]
	public void GivenTwoRecordsWithEqualListsWhenComparedThenAreNotEqual()
	{
		// Arrange — documents that records compare lists by reference (see PrinterCapabilities remarks)
		PrinterCapabilities first = new PrinterCapabilities { DuplexModes = [DuplexMode.OneSided] };
		PrinterCapabilities second = new PrinterCapabilities { DuplexModes = [DuplexMode.OneSided] };

		// Act
		bool areEqual = first.Equals(second);

		// Assert
		Assert.False(areEqual);
		Assert.Equal(first.DuplexModes, second.DuplexModes);
	}
}
