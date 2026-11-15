using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrinterInfoTests
{
	[Fact]
	public void GivenOnlyRequiredPropertiesWhenCreatedThenStateIsUnknownAndNotDefault()
	{
		// Arrange
		PrinterInfo info;

		// Act
		info = new PrinterInfo { Id = "cups:Office", Name = "Office" };

		// Assert
		Assert.Equal(PrinterState.Unknown, info.State);
		Assert.False(info.IsDefault);
		Assert.Null(info.Description);
		Assert.Null(info.Location);
		Assert.Null(info.MakeAndModel);
	}

	[Fact]
	public void GivenSameValuesWhenComparedThenAreEqual()
	{
		// Arrange
		PrinterInfo first = new PrinterInfo { Id = "cups:Office", Name = "Office", State = PrinterState.Idle };
		PrinterInfo second = new PrinterInfo { Id = "cups:Office", Name = "Office", State = PrinterState.Idle };

		// Act
		bool areEqual = first.Equals(second);

		// Assert
		Assert.True(areEqual);
	}
}
