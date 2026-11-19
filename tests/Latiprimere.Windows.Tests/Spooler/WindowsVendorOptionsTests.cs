using System.Buffers.Binary;
using System.Text;
using System.Xml;
using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Spooler;

public sealed class WindowsVendorOptionsTests
{
	private const int PublicSize = 220; // dmSize of a current DEVMODEW

	[Fact]
	public void GivenNoWindowsKeysWhenReadThenReturnsNullAndIgnoresTheOthers()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string>
		{
			["ipp:print-quality"] = "5",
			["cups:fit-to-page"]  = "true"
		};

		// Act
		byte[]? devMode = WindowsVendorOptions.Read(options).DevMode;

		// Assert
		Assert.Null(devMode);
	}

	[Fact]
	public void GivenSavedDevModeWhenReadThenReturnsItWithTheDriversData()
	{
		// Arrange — 220 public bytes and 8 of the driver's own
		byte[] saved = WindowsVendorOptionsTests.CreateDevMode(WindowsVendorOptionsTests.PublicSize, driverExtra: 8, length: 228);
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(saved) };

		// Act
		byte[]? devMode = WindowsVendorOptions.Read(options).DevMode;

		// Assert
		Assert.Equal(saved, devMode);
	}

	[Fact]
	public void GivenSavedDevModeInLongerMemoryBlockWhenReadThenAcceptsIt()
	{
		// Arrange — GlobalAlloc may round a block up, so the bytes can outlast the DEVMODE
		byte[] saved = WindowsVendorOptionsTests.CreateDevMode(WindowsVendorOptionsTests.PublicSize, driverExtra: 8, length: 240);
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(saved) };

		// Act
		byte[]? devMode = WindowsVendorOptions.Read(options).DevMode;

		// Assert
		Assert.Equal(240, devMode?.Length);
	}

	[Theory]
	[InlineData("windows:paper")]
	[InlineData("windows:DevMode")]
	public void GivenWindowsKeyWindowsDoesNotSupportWhenReadThenThrowsNotSupportedException(string key)
	{
		// Arrange — an unknown name, and keys are case-sensitive (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { [key] = "x" };

		// Act
		Action read = () => WindowsVendorOptions.Read(options);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(read);
		Assert.Contains($"'{key}'", exception.Message, StringComparison.Ordinal);
		Assert.Contains("windows:devmode", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenDevModeThatIsNotBase64WhenReadThenThrowsArgumentException()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = "not base64!" };

		// Act
		Action read = () => WindowsVendorOptions.Read(options);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(read);
		Assert.IsType<FormatException>(exception.InnerException);
	}

	[Theory]
	[InlineData(220, 8, 100)]
	[InlineData(220, 0, 219)]
	[InlineData(40, 0, 220)]
	[InlineData(0, 0, 220)]
	public void GivenBytesWhoseSizesDontAddUpWhenReadThenThrowsArgumentException(int size, int driverExtra, int length)
	{
		// Arrange — longer than the buffer, or too small a public part (provided by InlineData above)
		byte[] saved = WindowsVendorOptionsTests.CreateDevMode(size, driverExtra, length);
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(saved) };

		// Act
		Action read = () => WindowsVendorOptions.Read(options);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(read);
		Assert.Contains($"has {length} bytes", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(70)]
	public void GivenTooFewBytesToBeADevModeWhenReadThenThrowsArgumentException(int length)
	{
		// Arrange — not even dmSize and dmDriverExtra (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(new byte[length]) };

		// Act
		Action read = () => WindowsVendorOptions.Read(options);

		// Assert
		Assert.Throws<ArgumentException>(read);
	}

	[Fact]
	public void GivenNoWindowsKeysWhenReadThenReturnsNone()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string> { ["ipp:print-quality"] = "5" };

		// Act
		WindowsVendorSettings settings = WindowsVendorOptions.Read(options);

		// Assert
		Assert.Same(WindowsVendorSettings.None, settings);
		Assert.Null(settings.PrintTicket);
	}

	[Fact]
	public void GivenPrintTicketWhenReadThenReturnsItAsUtf8WithItsPrefixesKept()
	{
		// Arrange — a delta ticket asking for A5, declared as UTF-16, as a .NET string written out would be
		Dictionary<string, string> options = new Dictionary<string, string>
		{
			["windows:print-ticket"] = WindowsVendorOptionsTests.CreatePrintTicket("psk:ISOA5", declaration: "<?xml version=\"1.0\" encoding=\"utf-16\"?>")
		};

		// Act
		byte[]? ticket = WindowsVendorOptions.Read(options).PrintTicket;

		// Assert
		Assert.NotNull(ticket);
		string text = Encoding.UTF8.GetString(ticket);
		Assert.False(ticket.AsSpan().StartsWith(Encoding.UTF8.Preamble), "No byte order mark");
		Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", text, StringComparison.Ordinal);
		Assert.Contains("<psf:Feature name=\"psk:PageMediaSize\">", text, StringComparison.Ordinal);
		Assert.Contains("<psf:Option name=\"psk:ISOA5\"", text, StringComparison.Ordinal);
		Assert.Contains("xmlns:psk=\"http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords\"", text, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenDevModeAndPrintTicketWhenReadThenReturnsBoth()
	{
		// Arrange — the ticket is merged onto the saved DEVMODE
		byte[] saved = WindowsVendorOptionsTests.CreateDevMode(WindowsVendorOptionsTests.PublicSize, driverExtra: 0, length: 220);

		Dictionary<string, string> options = new Dictionary<string, string>
		{
			["windows:devmode"]      = Convert.ToBase64String(saved),
			["windows:print-ticket"] = WindowsVendorOptionsTests.CreatePrintTicket("psk:ISOA5")
		};

		// Act
		WindowsVendorSettings settings = WindowsVendorOptions.Read(options);

		// Assert
		Assert.Equal(saved, settings.DevMode);
		Assert.NotNull(settings.PrintTicket);
	}

	[Theory]
	[InlineData("")]
	[InlineData("duplex please")]
	[InlineData("<psf:PrintTicket xmlns:psf=\"http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework\">")]
	[InlineData("<psf:PrintTicket xmlns:psf=\"http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework\" /><second />")]
	[InlineData("<!DOCTYPE PrintTicket [<!ENTITY x \"x\">]><PrintTicket xmlns=\"http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework\" />")]
	public void GivenPrintTicketThatIsNotXmlWhenReadThenThrowsArgumentException(string ticket)
	{
		// Arrange — empty, text, unclosed, two roots, and a document type definition (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:print-ticket"] = ticket };

		// Act
		Action read = () => WindowsVendorOptions.Read(options);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(read);
		Assert.IsType<XmlException>(exception.InnerException);
		Assert.Contains("windows:print-ticket", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("<PrintTicket />", "'PrintTicket'")]
	[InlineData("<psf:PrintCapabilities xmlns:psf=\"http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework\" />", "'{http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework}PrintCapabilities'")]
	public void GivenXmlThatIsNotAPrintTicketWhenReadThenThrowsArgumentExceptionNamingTheRoot(string ticket, string root)
	{
		// Arrange — no namespace, and the wrong Print Schema document (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:print-ticket"] = ticket };

		// Act
		Action read = () => WindowsVendorOptions.Read(options);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(read);
		Assert.Contains(root, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenFeatureAndParameterKeysWhenReadThenKeepsThemForThePrintersCapabilities()
	{
		// Arrange — whether they exist is only known from the printer's Print Capabilities, when printed
		Dictionary<string, string> options = new Dictionary<string, string>
		{
			["windows:feature:psk:PageOutputQuality"] = "psk:High",
			["windows:parameter:ns0000:JobDarkness"]  = "2",
			["ipp:print-quality"]                     = "5"
		};

		// Act
		WindowsVendorSettings settings = WindowsVendorOptions.Read(options);

		// Assert
		Assert.Equal(2, settings.Features.Count);
		Assert.Equal("psk:High", settings.Features["windows:feature:psk:PageOutputQuality"]);
		Assert.Null(settings.DevMode);
		Assert.Null(settings.PrintTicket);
	}

	[Theory]
	[InlineData("windows:feature:PageOutputQuality", "psk:High")]
	[InlineData("windows:feature:psk:", "psk:High")]
	[InlineData("windows:feature:psk:Page Quality", "psk:High")]
	[InlineData("windows:parameter:ns0000:JobDarkness", "")]
	public void GivenFeatureKeyThatIsNotAQualifiedNameWhenReadThenThrowsArgumentException(string key, string value)
	{
		// Arrange — no prefix, no local name, a space, and no value (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { [key] = value };

		// Act
		Action read = () => WindowsVendorOptions.Read(options);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(read);
		Assert.Contains(key, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenUnsupportedKeyWhenReadThenTheMessageNamesTheFeatureKeys()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string> { ["windows:quality"] = "high" };

		// Act
		Action read = () => WindowsVendorOptions.Read(options);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(read);
		Assert.Contains("windows:feature:<name>", exception.Message, StringComparison.Ordinal);
		Assert.Contains("windows:parameter:<name>", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Creates a delta Print Ticket that chooses one paper size.
	/// </summary>
	/// <param name="mediaSize">The PageMediaSize option, such as <c>psk:ISOA5</c>.</param>
	/// <param name="declaration">An XML declaration to start with, or none.</param>
	/// <returns>The ticket.</returns>
	internal static string CreatePrintTicket(string mediaSize, string declaration = "")
	{
		return declaration + $"""
			<psf:PrintTicket xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework" xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords" version="1">
			  <psf:Feature name="psk:PageMediaSize">
			    <psf:Option name="{mediaSize}" />
			  </psf:Feature>
			</psf:PrintTicket>
			""";
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
