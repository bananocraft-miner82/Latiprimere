using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Mapping;
using Xunit;

namespace Latiprimere.Windows.Tests.Mapping;

public sealed class WindowsErrorMapperTests
{
	private const string Office = "Office";                    // A local printer
	private const string SharedOffice = @"\\server\Office";    // A printer shared by a print server
	private const string WindowsText = "Windows' own description."; // Stands in for the Win32Exception message

	[Fact]
	public void GivenUnexplainedErrorWhenMappedThenPrintExceptionNamesCallPrinterAndWindowsText()
	{
		// Arrange — ERROR_NOT_ENOUGH_MEMORY, which Windows' own text explains well enough
		const int notEnoughMemory = 8;

		// Act
		Exception exception = WindowsErrorMapperTests.Map(notEnoughMemory, "StartDocPrinter", WindowsErrorMapperTests.Office);

		// Assert
		PrintException printException = Assert.IsType<PrintException>(exception);
		Assert.Equal(notEnoughMemory, printException.NativeErrorCode);

		Assert.Equal
		(
			"The Windows print spooler call 'StartDocPrinter' failed for printer 'Office': Windows' own description.",
			printException.Message
		);
	}

	[Theory]
	[InlineData(Win32ErrorCodes.AccessDenied, "Windows denied access")]
	[InlineData(Win32ErrorCodes.UnknownPrinterDriver, "driver isn't installed")]
	[InlineData(Win32ErrorCodes.PrintCancelled, "deleted from the print queue while it was being sent")]
	[InlineData(Win32ErrorCodes.Cancelled, "PrintOptions.OutputFile")]
	[InlineData(Win32ErrorCodes.FileNotFound, "check that its folder exists")]
	[InlineData(Win32ErrorCodes.PathNotFound, "check that its folder exists")]
	[InlineData(Win32ErrorCodes.DiskFull, "is full")]
	[InlineData(Win32ErrorCodes.HandleDiskFull, "is full")]
	public void GivenExplainedErrorWhenMappedThenPrintExceptionLeadsWithExplanationAndKeepsWindowsText(int win32Error, string explanation)
	{
		// Arrange — win32Error is an error Windows' text doesn't explain well, and explanation part of what Latiprimere
		// adds (provided by InlineData above)
		string printerName = WindowsErrorMapperTests.Office;

		// Act
		Exception exception = WindowsErrorMapperTests.Map(win32Error, "StartDocPrinter", printerName);

		// Assert
		PrintException printException = Assert.IsType<PrintException>(exception);
		Assert.Equal(win32Error, printException.NativeErrorCode);
		Assert.Contains(explanation, printException.Message, StringComparison.Ordinal);
		Assert.EndsWith(WindowsErrorMapperTests.WindowsText, printException.Message, StringComparison.Ordinal);

		Assert.True
		(
			printException.Message.IndexOf(explanation, StringComparison.Ordinal)
				< printException.Message.IndexOf("StartDocPrinter", StringComparison.Ordinal),
			"The explanation should come before the failed call."
		);
	}

	[Theory]
	[InlineData(Win32ErrorCodes.RpcServerUnavailable)]
	[InlineData(Win32ErrorCodes.RpcCallFailed)]
	[InlineData(Win32ErrorCodes.RpcCallFailedDidNotExecute)]
	public void GivenLocalSpoolerUnreachableWhenMappedThenPrintSystemUnavailableSaysToStartTheService(int win32Error)
	{
		// Arrange — win32Error is one of the RPC failures (provided by InlineData above); listing printers names none
		string printerName = string.Empty;

		// Act
		Exception exception = WindowsErrorMapperTests.Map(win32Error, "EnumPrintersW", printerName);

		// Assert
		PrintSystemUnavailableException unavailable = Assert.IsType<PrintSystemUnavailableException>(exception);
		Assert.Equal(win32Error, unavailable.NativeErrorCode);
		Assert.Contains("Print Spooler service isn't running", unavailable.Message, StringComparison.Ordinal);
		Assert.Contains("The Windows print spooler call 'EnumPrintersW' failed: ", unavailable.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenSharedPrinterUnreachableWhenMappedThenPrintExceptionBlamesThePrintServer()
	{
		// Arrange — the RPC failure is the print server's, not this computer's spooler
		const int rpcServerUnavailable = Win32ErrorCodes.RpcServerUnavailable;

		// Act
		Exception exception = WindowsErrorMapperTests.Map(rpcServerUnavailable, "OpenPrinterW", WindowsErrorMapperTests.SharedOffice);

		// Assert
		PrintException printException = Assert.IsType<PrintException>(exception);
		Assert.Equal(rpcServerUnavailable, printException.NativeErrorCode);
		Assert.Contains("print server sharing this printer can't be reached", printException.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(Win32ErrorCodes.InvalidPrinterName)]
	[InlineData(Win32ErrorCodes.PrinterDeleted)]
	[InlineData(Win32ErrorCodes.PrinterNotFound)]
	public void GivenPrinterGoneWhenMappedThenPrinterNotFoundWithItsId(int win32Error)
	{
		// Arrange — win32Error is one of the ways Windows says the printer is gone (provided by InlineData above)
		string printerName = WindowsErrorMapperTests.Office;

		// Act
		Exception exception = WindowsErrorMapperTests.Map(win32Error, "OpenPrinterW", printerName);

		// Assert
		PrinterNotFoundException notFound = Assert.IsType<PrinterNotFoundException>(exception);
		Assert.Equal("windows:Office", notFound.PrinterId);
		Assert.Equal(win32Error, notFound.NativeErrorCode);
		Assert.Contains("removed from Windows", notFound.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenDriverRefusingRawDataWhenMappedThenNotSupportedWithTheReason()
	{
		// Arrange
		const int invalidDataType = Win32ErrorCodes.InvalidDataType;

		// Act
		Exception exception = WindowsErrorMapperTests.Map(invalidDataType, "StartDocPrinterW", WindowsErrorMapperTests.Office);

		// Assert — the IPrinter contract promises NotSupportedException for a format the printer can't accept
		NotSupportedException notSupported = Assert.IsType<NotSupportedException>(exception);
		Assert.Contains("doesn't accept raw printer data", notSupported.Message, StringComparison.Ordinal);
		Assert.Contains("StartDocPrinterW", notSupported.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenNoErrorCodeWhenMappedThenSaysWindowsGaveNoReasonInsteadOfSuccess()
	{
		// Arrange — Windows describes error 0 as "The operation completed successfully"
		const string successText = "The operation completed successfully.";

		// Act
		Exception exception = WindowsErrorMapper.ToException
		(
			Win32ErrorCodes.Success,
			"EndDoc",
			WindowsErrorMapperTests.Office,
			successText
		);

		// Assert
		PrintException printException = Assert.IsType<PrintException>(exception);
		Assert.Equal(0, printException.NativeErrorCode);
		Assert.DoesNotContain(successText, printException.Message, StringComparison.Ordinal);
		Assert.EndsWith("Windows gave no reason.", printException.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenPrinterNotFoundForCallWithoutPrinterWhenMappedThenHasNoPrinterId()
	{
		// Arrange
		string printerName = string.Empty;

		// Act
		Exception exception = WindowsErrorMapperTests.Map(Win32ErrorCodes.PrinterNotFound, "GetDefaultPrinterW", printerName);

		// Assert
		PrinterNotFoundException notFound = Assert.IsType<PrinterNotFoundException>(exception);
		Assert.Null(notFound.PrinterId);
	}

	/// <summary>
	/// Maps an error, with a stand-in for Windows' own description.
	/// </summary>
	/// <param name="win32Error">The Win32 error code.</param>
	/// <param name="functionName">The Win32 function that failed.</param>
	/// <param name="printerName">The printer it was called for, or empty.</param>
	/// <returns>The exception.</returns>
	private static Exception Map(int win32Error, string functionName, string printerName)
	{
		return WindowsErrorMapper.ToException(win32Error, functionName, printerName, WindowsErrorMapperTests.WindowsText);
	}
}
