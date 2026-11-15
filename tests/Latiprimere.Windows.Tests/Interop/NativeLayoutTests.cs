using System.Runtime.InteropServices;
using Latiprimere.Windows.Interop;
using Xunit;

namespace Latiprimere.Windows.Tests.Interop;

public sealed class NativeLayoutTests
{
	[Fact]
	public void GivenPrinterInfo2WhenMeasuredThenMatchesNativeSize()
	{
		// Arrange — 13 pointers followed by 8 DWORDs, as in winspool.h
		int expectedSize = (13 * IntPtr.Size) + (8 * sizeof(uint));

		// Act
		int size = Marshal.SizeOf<PrinterInfo2>();

		// Assert
		Assert.Equal(expectedSize, size);
	}

	[Fact]
	public void GivenJobInfo1WhenMeasuredThenSubmittedTimeIsNotPaddedAndSizeMatchesNative()
	{
		// Arrange — JobId, padding on 64-bit, 6 pointers, 5 DWORDs, then SYSTEMTIME (8 WORDs)
		int pointerBlockStart = IntPtr.Size == 8 ? 8 : 4;
		int expectedSubmittedOffset = pointerBlockStart + (6 * IntPtr.Size) + (5 * sizeof(uint));
		int unpaddedSize = expectedSubmittedOffset + (8 * sizeof(ushort));
		int expectedSize = (unpaddedSize + IntPtr.Size - 1) / IntPtr.Size * IntPtr.Size;

		// Act
		int submittedOffset = (int)Marshal.OffsetOf<JobInfo1>(nameof(JobInfo1.SubmittedYear));
		int size = Marshal.SizeOf<JobInfo1>();

		// Assert
		Assert.Equal(expectedSubmittedOffset, submittedOffset);
		Assert.Equal(expectedSize, size);
	}

	[Fact]
	public void GivenDocInfo1WhenMeasuredThenMatchesNativeSize()
	{
		// Arrange — three pointers
		int expectedSize = 3 * IntPtr.Size;

		// Act
		int size = Marshal.SizeOf<DocInfo1>();

		// Assert
		Assert.Equal(expectedSize, size);
	}

	[Theory]
	[InlineData(nameof(DevMode.Fields), 72)]
	[InlineData(nameof(DevMode.Orientation), 76)]
	[InlineData(nameof(DevMode.Copies), 86)]
	[InlineData(nameof(DevMode.Duplex), 94)]
	[InlineData(nameof(DevMode.Collate), 100)]
	[InlineData(nameof(DevMode.LogPixels), 166)]
	[InlineData(nameof(DevMode.BitsPerPixel), 168)]
	public void GivenDevModeFieldWhenOffsetMeasuredThenMatchesWingdi(string fieldName, int expectedOffset)
	{
		// Arrange — DEVMODEW field offsets from wingdi.h (provided by InlineData above)
		string field = fieldName;

		// Act
		int offset = (int)Marshal.OffsetOf<DevMode>(field);

		// Assert
		Assert.Equal(expectedOffset, offset);
	}

	[Fact]
	public void GivenDevModeWhenMeasuredThenIsThePublic220Bytes()
	{
		// Arrange
		const int expectedSize = 220;

		// Act
		int size = Marshal.SizeOf<DevMode>();

		// Assert
		Assert.Equal(expectedSize, size);
	}

	[Fact]
	public void GivenGdiDocInfoWhenMeasuredThenMatchesNativeSize()
	{
		// Arrange — an int, three pointers and a DWORD, padded to pointer alignment
		int unpadded = IntPtr.Size + (3 * IntPtr.Size) + sizeof(uint);
		int expectedSize = (unpadded + IntPtr.Size - 1) / IntPtr.Size * IntPtr.Size;

		// Act
		int size = Marshal.SizeOf<GdiDocInfo>();

		// Assert
		Assert.Equal(expectedSize, size);
	}

	[Fact]
	public void GivenBitmapInfoHeaderWhenMeasuredThenIs40Bytes()
	{
		// Arrange
		const int expectedSize = 40;

		// Act
		int size = Marshal.SizeOf<BitmapInfoHeader>();

		// Assert
		Assert.Equal(expectedSize, size);
	}
}
