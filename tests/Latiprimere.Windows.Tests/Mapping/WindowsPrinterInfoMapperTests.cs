using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Mapping;
using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Mapping;

public sealed class WindowsPrinterInfoMapperTests
{
	[Fact]
	public void GivenPrinterRecordWhenToPrinterInfoThenMapsFields()
	{
		// Arrange
		WindowsPrinterRecord printer = new WindowsPrinterRecord
		{
			Name       = "Office",
			Comment    = "Second floor laser",
			Location   = "Room 2.14",
			DriverName = "HP PCL3 A-size Printer Class Driver",
			Status     = PrinterStatusFlags.None
		};

		// Act
		PrinterInfo info = WindowsPrinterInfoMapper.ToPrinterInfo(printer, "windows:Office", true);

		// Assert
		Assert.Equal("windows:Office", info.Id);
		Assert.Equal("Office", info.Name);
		Assert.Equal("Second floor laser", info.Description);
		Assert.Equal("Room 2.14", info.Location);
		Assert.Equal("HP PCL3 A-size Printer Class Driver", info.MakeAndModel);
		Assert.Equal(PrinterState.Idle, info.State);
		Assert.True(info.IsDefault);
	}

	[Fact]
	public void GivenBlankDetailsWhenToPrinterInfoThenDetailsAreNull()
	{
		// Arrange
		WindowsPrinterRecord printer = new WindowsPrinterRecord { Name = "Office", Comment = "", Location = " " };

		// Act
		PrinterInfo info = WindowsPrinterInfoMapper.ToPrinterInfo(printer, "windows:Office", false);

		// Assert
		Assert.Null(info.Description);
		Assert.Null(info.Location);
		Assert.Null(info.MakeAndModel);
	}

	[Theory]
	[InlineData(0x00000000u, PrinterState.Idle)]        // ready
	[InlineData(0x00020000u, PrinterState.Idle)]        // toner low (advisory)
	[InlineData(0x01000000u, PrinterState.Idle)]        // power save (advisory)
	[InlineData(0x00000400u, PrinterState.Processing)]  // printing
	[InlineData(0x00000200u, PrinterState.Processing)]  // busy
	[InlineData(0x00010000u, PrinterState.Processing)]  // warming up
	[InlineData(0x00000001u, PrinterState.Stopped)]     // paused
	[InlineData(0x00000080u, PrinterState.Stopped)]     // offline
	[InlineData(0x00000010u, PrinterState.Stopped)]     // paper out
	[InlineData(0x00400000u, PrinterState.Stopped)]     // door open
	[InlineData(0x00000480u, PrinterState.Stopped)]     // offline while printing
	public void GivenSpoolerStatusFlagsWhenToPrinterStateThenReturnsState(uint statusFlags, PrinterState expectedState)
	{
		// Arrange — PRINTER_STATUS_* flag combinations and the expected state (provided by InlineData above)
		PrinterStatusFlags status = (PrinterStatusFlags)statusFlags;

		// Act
		PrinterState state = WindowsPrinterInfoMapper.ToPrinterState(status, false);

		// Assert
		Assert.Equal(expectedState, state);
	}

	[Fact]
	public void GivenPrinterSetToWorkOfflineWhenToPrinterStateThenReturnsStopped()
	{
		// Arrange
		PrinterStatusFlags status = PrinterStatusFlags.None;

		// Act
		PrinterState state = WindowsPrinterInfoMapper.ToPrinterState(status, isWorkingOffline: true);

		// Assert
		Assert.Equal(PrinterState.Stopped, state);
	}
}
