using System.Buffers.Binary;
using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Spooler;

public sealed class WindowsVendorOptionsTests
{
	private const int PublicSize = 220; // dmSize of a current DEVMODEW

	[Fact]
	public void GivenNoWindowsKeysWhenReadDevModeThenReturnsNullAndIgnoresTheOthers()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string>
		{
			["ipp:print-quality"] = "5",
			["cups:fit-to-page"]  = "true"
		};

		// Act
		byte[]? devMode = WindowsVendorOptions.ReadDevMode(options);

		// Assert
		Assert.Null(devMode);
	}

	[Fact]
	public void GivenSavedDevModeWhenReadDevModeThenReturnsItWithTheDriversData()
	{
		// Arrange — 220 public bytes and 8 of the driver's own
		byte[] saved = WindowsVendorOptionsTests.CreateDevMode(WindowsVendorOptionsTests.PublicSize, driverExtra: 8, length: 228);
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(saved) };

		// Act
		byte[]? devMode = WindowsVendorOptions.ReadDevMode(options);

		// Assert
		Assert.Equal(saved, devMode);
	}

	[Fact]
	public void GivenSavedDevModeInLongerMemoryBlockWhenReadDevModeThenAcceptsIt()
	{
		// Arrange — GlobalAlloc may round a block up, so the bytes can outlast the DEVMODE
		byte[] saved = WindowsVendorOptionsTests.CreateDevMode(WindowsVendorOptionsTests.PublicSize, driverExtra: 8, length: 240);
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(saved) };

		// Act
		byte[]? devMode = WindowsVendorOptions.ReadDevMode(options);

		// Assert
		Assert.Equal(240, devMode?.Length);
	}

	[Theory]
	[InlineData("windows:print-ticket")]
	[InlineData("windows:DevMode")]
	public void GivenWindowsKeyWindowsDoesNotSupportWhenReadDevModeThenThrowsNotSupportedException(string key)
	{
		// Arrange — the Print Ticket isn't supported yet, and keys are case-sensitive (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { [key] = "x" };

		// Act
		Action read = () => WindowsVendorOptions.ReadDevMode(options);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(read);
		Assert.Contains($"'{key}'", exception.Message, StringComparison.Ordinal);
		Assert.Contains("windows:devmode", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenDevModeThatIsNotBase64WhenReadDevModeThenThrowsArgumentException()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = "not base64!" };

		// Act
		Action read = () => WindowsVendorOptions.ReadDevMode(options);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(read);
		Assert.IsType<FormatException>(exception.InnerException);
	}

	[Theory]
	[InlineData(220, 8, 100)]
	[InlineData(220, 0, 219)]
	[InlineData(40, 0, 220)]
	[InlineData(0, 0, 220)]
	public void GivenBytesWhoseSizesDontAddUpWhenReadDevModeThenThrowsArgumentException(int size, int driverExtra, int length)
	{
		// Arrange — longer than the buffer, or too small a public part (provided by InlineData above)
		byte[] saved = WindowsVendorOptionsTests.CreateDevMode(size, driverExtra, length);
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(saved) };

		// Act
		Action read = () => WindowsVendorOptions.ReadDevMode(options);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(read);
		Assert.Contains($"has {length} bytes", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(70)]
	public void GivenTooFewBytesToBeADevModeWhenReadDevModeThenThrowsArgumentException(int length)
	{
		// Arrange — not even dmSize and dmDriverExtra (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(new byte[length]) };

		// Act
		Action read = () => WindowsVendorOptions.ReadDevMode(options);

		// Assert
		Assert.Throws<ArgumentException>(read);
	}

	/// <summary>
	/// Creates bytes with a DEVMODE's size fields set.
	/// </summary>
	/// <param name="size">The <c>dmSize</c> to write.</param>
	/// <param name="driverExtra">The <c>dmDriverExtra</c> to write.</param>
	/// <param name="length">The number of bytes.</param>
	/// <returns>The bytes.</returns>
	private static byte[] CreateDevMode(int size, int driverExtra, int length)
	{
		byte[] devMode = new byte[Math.Max(length, 72)];
		BinaryPrimitives.WriteUInt16LittleEndian(devMode.AsSpan(68), (ushort)size);
		BinaryPrimitives.WriteUInt16LittleEndian(devMode.AsSpan(70), (ushort)driverExtra);

		return devMode[..length];
	}
}
