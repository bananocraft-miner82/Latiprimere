using Latiprimere.Ipp.Discovery;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Latiprimere.Ipp.Tests.Discovery;

public sealed class IppAddressStoreTests : IDisposable
{
	private readonly string directory = Path.Combine(Path.GetTempPath(), "latiprimere-tests", Guid.NewGuid().ToString("N")); // This test's folder

	[Fact]
	public void GivenPrintersSavedWhenLoadedThenTheyComeBackAsSaved()
	{
		// Arrange — in a folder that doesn't exist yet
		string path = Path.Combine(this.directory, "store", "printers.json");
		IppAddressStore store = new IppAddressStore(path, NullLogger.Instance);

		DiscoveredIppPrinter office = new DiscoveredIppPrinter
		{
			InstanceName = "Office Laser",
			PrinterUri   = new Uri("ipp://192.168.1.20:631/ipp/print"),
			SecureUri    = new Uri("ipps://192.168.1.20:443/ipp/print"),
			Uuid         = "1c852a4d-b800-1f08-abcd-a0d3c1d9e51b",
			MakeAndModel = "HP ENVY 5640 series",
			Location     = "Floor 2"
		};

		DiscoveredIppPrinter label = new DiscoveredIppPrinter { InstanceName = "Labels", PrinterUri = new Uri("ipps://192.168.1.30:631/ipp/print") };

		// Act
		store.Save([office, label]);
		IReadOnlyList<DiscoveredIppPrinter> loaded = new IppAddressStore(path, NullLogger.Instance).Load();

		// Assert
		Assert.Equal(2, loaded.Count);
		Assert.Contains(office, loaded);
		Assert.Contains(label, loaded);
		Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
	}

	[Fact]
	public void GivenNoFileWhenLoadedThenNothingIsRemembered()
	{
		// Arrange
		IppAddressStore store = new IppAddressStore(Path.Combine(this.directory, "missing.json"), NullLogger.Instance);

		// Act
		IReadOnlyList<DiscoveredIppPrinter> loaded = store.Load();

		// Assert
		Assert.Empty(loaded);
	}

	[Theory]
	[InlineData("not json")]
	[InlineData("{\"version\": 1, \"printers\": 5}")]
	[InlineData("")]
	public void GivenUnreadableFileWhenLoadedThenNothingIsRememberedAndNothingThrows(string content)
	{
		// Arrange (provided by InlineData above)
		string path = this.WriteFile(content);

		// Act
		IReadOnlyList<DiscoveredIppPrinter> loaded = new IppAddressStore(path, NullLogger.Instance).Load();

		// Assert
		Assert.Empty(loaded);
	}

	[Fact]
	public void GivenEntriesThatMakeNoSenseWhenLoadedThenOnlyTheGoodOnesAreKept()
	{
		// Arrange — no name, an http address, a bad IPPS address, and one good entry with an announced-form UUID
		string path = this.WriteFile
		(
			"""
			{
			  "version": 1,
			  "printers": [
			    { "name": "", "printerUri": "ipp://192.168.1.1/ipp/print" },
			    { "name": "Web", "printerUri": "http://192.168.1.2/ipp/print" },
			    { "name": "Half", "printerUri": "ipp://192.168.1.3/ipp/print", "secureUri": "https://192.168.1.3/ipp/print" },
			    { "name": "Good", "printerUri": "ipp://192.168.1.4/ipp/print", "uuid": "urn:uuid:ABCDEF00-0000-0000-0000-000000000001" }
			  ]
			}
			"""
		);

		// Act
		IReadOnlyList<DiscoveredIppPrinter> loaded = new IppAddressStore(path, NullLogger.Instance).Load();

		// Assert
		DiscoveredIppPrinter good = Assert.Single(loaded);
		Assert.Equal("Good", good.InstanceName);
		Assert.Equal("abcdef00-0000-0000-0000-000000000001", good.Uuid);
	}

	[Fact]
	public void GivenEntriesAtAddressesNoPrinterHasWhenLoadedThenTheyAreLeftOut()
	{
		// Arrange — loopback, a cloud metadata service and localhost, as an earlier version may have stored from
		// announcements, beside a printer that was reached over IPPS
		string path = this.WriteFile
		(
			"""
			{
			  "version": 1,
			  "printers": [
			    { "name": "Loopback", "printerUri": "ipp://127.0.0.1:631/ipp/print" },
			    { "name": "Metadata", "printerUri": "ipp://169.254.169.254:80/latest" },
			    { "name": "Local", "printerUri": "ipp://localhost:631/ipp/print" },
			    { "name": "Half", "printerUri": "ipp://192.168.1.3/ipp/print", "secureUri": "ipps://[::1]:443/ipp/print" },
			    { "name": "Good", "printerUri": "ipp://192.168.1.4/ipp/print", "reachedOverIpps": true }
			  ]
			}
			"""
		);

		// Act
		IReadOnlyList<DiscoveredIppPrinter> loaded = new IppAddressStore(path, NullLogger.Instance).Load();

		// Assert
		DiscoveredIppPrinter good = Assert.Single(loaded);
		Assert.Equal("Good", good.InstanceName);
		Assert.True(good.ReachedOverIpps);
	}

	[Fact]
	public void GivenNullEntriesWhenLoadedThenTheyAreSkippedAndNothingThrows()
	{
		// Arrange — JSON's null where an entry belongs, around a good one
		string path = this.WriteFile
		(
			"""
			{ "version": 1, "printers": [ null, { "name": "Good", "printerUri": "ipp://192.168.1.4/ipp/print" }, null ] }
			"""
		);

		// Act
		IReadOnlyList<DiscoveredIppPrinter> loaded = new IppAddressStore(path, NullLogger.Instance).Load();

		// Assert
		DiscoveredIppPrinter good = Assert.Single(loaded);
		Assert.Equal("Good", good.InstanceName);
	}

	[Fact]
	public void GivenAFileInALaterFormatWhenLoadedAndSavedThenNothingIsRememberedAndTheFileIsLeftAsItIs()
	{
		// Arrange — written by a later version of Latiprimere sharing the file
		const string content = """{ "version": 2, "printers": [ { "name": "Office", "printerUri": "ipp://192.168.1.20/ipp/print" } ] }""";
		string path = this.WriteFile(content);
		IppAddressStore store = new IppAddressStore(path, NullLogger.Instance);

		// Act
		IReadOnlyList<DiscoveredIppPrinter> loaded = store.Load();
		store.Save([new DiscoveredIppPrinter { InstanceName = "Other", PrinterUri = new Uri("ipp://192.168.1.30/ipp/print") }]);

		// Assert
		Assert.Empty(loaded);
		Assert.Equal(content, File.ReadAllText(path));
	}

	[Fact]
	public void GivenAFileWithoutAVersionWhenLoadedThenItIsReadAsTheCurrentFormat()
	{
		// Arrange
		string path = this.WriteFile("""{ "printers": [ { "name": "Office", "printerUri": "ipp://192.168.1.20/ipp/print" } ] }""");

		// Act
		IReadOnlyList<DiscoveredIppPrinter> loaded = new IppAddressStore(path, NullLogger.Instance).Load();

		// Assert
		Assert.Equal("Office", Assert.Single(loaded).InstanceName);
	}

	[Fact]
	public void GivenAPathThatCantBeWrittenWhenSavedThenNothingThrows()
	{
		// Arrange — the path is a folder
		Directory.CreateDirectory(this.directory);
		IppAddressStore store = new IppAddressStore(this.directory, NullLogger.Instance);

		// Act
		Exception? exception = Record.Exception(() => store.Save([new DiscoveredIppPrinter { InstanceName = "Office", PrinterUri = new Uri("ipp://192.168.1.20/ipp/print") }]));

		// Assert
		Assert.Null(exception);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (Directory.Exists(this.directory))
		{
			Directory.Delete(this.directory, recursive: true);
		}
	}

	/// <summary>
	/// Writes a store file with the given content.
	/// </summary>
	/// <param name="content">The file's text.</param>
	/// <returns>The file's path.</returns>
	private string WriteFile(string content)
	{
		Directory.CreateDirectory(this.directory);
		string path = Path.Combine(this.directory, "printers.json");
		File.WriteAllText(path, content);

		return path;
	}
}
