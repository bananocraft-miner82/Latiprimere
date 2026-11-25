using Latiprimere.Abstractions.Tests.Fakes;
using Xunit;

namespace Latiprimere.Abstractions.Tests;

/// <summary>
/// A printer's details, and the default implementations of the members ADR 0059 added to <see cref="IPrinter"/>, which
/// a printer written for an earlier version gets.
/// </summary>
public sealed class PrinterDetailsTests
{
	[Fact]
	public void GivenOnlyThePrinterIdWhenCreatedThenNothingElseIsReported()
	{
		// Arrange
		PrinterDetails details;

		// Act
		details = new PrinterDetails { PrinterId = "ipp:office" };

		// Assert
		Assert.Null(details.WebPage);
		Assert.Null(details.SuppliesWebPage);
		Assert.Empty(details.Icons);
		Assert.Empty(details.Firmware);
		Assert.Empty(details.IdentifyActions);
	}

	[Fact]
	public void GivenFirmwareOfTheSameNameAndVersionWhenComparedThenTheyAreEqual()
	{
		// Arrange
		PrinterFirmware first = new PrinterFirmware { Name = "Firmware", Version = "2.4.1" };
		PrinterFirmware second = new PrinterFirmware { Name = "Firmware", Version = "2.4.1" };

		// Act
		bool areEqual = first.Equals(second);

		// Assert
		Assert.True(areEqual);
	}

	[Fact]
	public async Task GivenAPrinterWrittenForAnEarlierVersionWhenItsDetailsAreReadThenTheyHaveOnlyItsId()
	{
		// Arrange
		IPrinter printer = new MinimalPrinter();

		// Act
		PrinterDetails details = await printer.GetDetailsAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("own:office", details.PrinterId);
		Assert.Null(details.WebPage);
		Assert.Empty(details.IdentifyActions);
	}

	[Fact]
	public async Task GivenAPrinterWrittenForAnEarlierVersionWhenIdentifiedThenThrowsNotSupportedNamingIt()
	{
		// Arrange
		IPrinter printer = new MinimalPrinter();

		// Act
		Func<Task> identify = () => printer.IdentifyAsync("Here", TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(identify);
		Assert.Contains("'own:office'", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenAPrinterWrittenForAnEarlierVersionWhenATestPageIsPrintedThenThrowsNotSupportedNamingIt()
	{
		// Arrange
		IPrinter printer = new MinimalPrinter();

		// Act
		Func<Task> print = () => printer.PrintTestPageAsync(PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(print);
		Assert.Contains("'own:office'", exception.Message, StringComparison.Ordinal);
	}
}
