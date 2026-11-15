using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrinterNotFoundExceptionTests
{
	[Fact]
	public void GivenPrinterIdWhenFromPrinterIdThenRecordsIdAndNamesItInMessage()
	{
		// Arrange
		const string printerId = "Office-Laser";

		// Act
		PrinterNotFoundException exception = PrinterNotFoundException.FromPrinterId(printerId);

		// Assert
		Assert.Equal(printerId, exception.PrinterId);
		Assert.Contains(printerId, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenNoArgumentsWhenCreatedThenHasDefaultMessageAndNoPrinterId()
	{
		// Arrange
		PrinterNotFoundException exception;

		// Act
		exception = new PrinterNotFoundException();

		// Assert
		Assert.False(string.IsNullOrWhiteSpace(exception.Message));
		Assert.Null(exception.PrinterId);
	}

	[Fact]
	public void GivenPrinterNotFoundExceptionWhenCaughtAsPrintExceptionThenIsCaught()
	{
		// Arrange
		PrinterNotFoundException exception = PrinterNotFoundException.FromPrinterId("Missing");

		// Act
		bool isPrintException = exception is PrintException;

		// Assert
		Assert.True(isPrintException);
	}
}
