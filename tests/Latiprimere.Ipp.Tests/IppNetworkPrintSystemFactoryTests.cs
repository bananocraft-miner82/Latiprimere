using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Latiprimere.Ipp.Connections;
using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppNetworkPrintSystemFactoryTests
{
	[Fact]
	public void GivenNoOptionsWhenCreateThenPrintSystemIsSupportedAndDisposable()
	{
		// Arrange — nothing to arrange: the defaults use discovery only

		// Act
		IPrintSystem printSystem = IppNetworkPrintSystemFactory.Create();

		// Assert
		Assert.Equal("IPP", printSystem.Name);
		Assert.True(printSystem.IsSupported);
		Assert.IsAssignableFrom<IDisposable>(printSystem);
		((IDisposable)printSystem).Dispose();
	}

	[Fact]
	public async Task GivenConfiguredPrinterWithoutDiscoveryWhenGetPrintersThenListsItWithoutNetworkAccess()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions { EnableDiscovery = false };
		options.PrinterUris.Add(new Uri("ipp://192.0.2.10/ipp/print"));
		IPrintSystem printSystem = IppNetworkPrintSystemFactory.Create(options);

		// Act
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrinterInfo printer = Assert.Single(printers);
		Assert.Equal("ipp:ipp://192.0.2.10/ipp/print", printer.Id);
		((IDisposable)printSystem).Dispose();
	}

	[Fact]
	public void GivenFingerprintThatIsntSha256WhenCreateThenThrowsArgumentException()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions { EnableDiscovery = false };
		options.TrustedCertificateFingerprints.Add("3A:7F:00");

		// Act
		Action create = () => IppNetworkPrintSystemFactory.Create(options).Dispose();

		// Assert
		Assert.Throws<ArgumentException>(create);
	}

	[Theory]
	[InlineData("", "secret")]
	[InlineData("ann:smith", "secret")]
	public void GivenCredentialsBasicAuthenticationCantCarryWhenCreateThenThrowsArgumentException(string userName, string password)
	{
		// Arrange (provided by InlineData above)
		IppNetworkOptions options = new IppNetworkOptions { EnableDiscovery = false, Credentials = new System.Net.NetworkCredential(userName, password) };

		// Act
		Action create = () => IppNetworkPrintSystemFactory.Create(options).Dispose();

		// Assert
		Assert.Throws<ArgumentException>(create);
	}

	[Theory]
	[InlineData("http://192.168.1.20/ipp/print")]
	[InlineData("ipp://192.168.1.20/ipp/print")]
	public void GivenPrinterCredentialsWhenCreateThenOnlyIppAndIppsUrisAreAccepted(string uri)
	{
		// Arrange (provided by InlineData above)
		IppNetworkOptions options = new IppNetworkOptions { EnableDiscovery = false };
		options.PrinterCredentials[new Uri(uri)] = new System.Net.NetworkCredential("ann", "secret");

		// Act
		Exception? exception = Record.Exception(() => IppNetworkPrintSystemFactory.Create(options).Dispose());

		// Assert
		Assert.Equal(uri.StartsWith("ipp", StringComparison.Ordinal), exception is null);
	}

	[Fact]
	public void GivenPrinterCredentialsForARelativeUriWhenCreateThenThrowsArgumentException()
	{
		// Arrange — with shared credentials too, so the configured hosts are gathered
		IppNetworkOptions options = new IppNetworkOptions { EnableDiscovery = false, Credentials = new System.Net.NetworkCredential("ann", "secret") };
		options.PrinterCredentials[new Uri("ipp/print", UriKind.Relative)] = new System.Net.NetworkCredential("bob", "secret");

		// Act
		Action create = () => IppNetworkPrintSystemFactory.Create(options).Dispose();

		// Assert
		Assert.Throws<ArgumentException>(create);
	}

	[Theory]
	[InlineData(true, "192.168.1.20", true)]
	[InlineData(true, "192.168.1.21", true)]
	[InlineData(true, "printer.example.com", false)]
	[InlineData(false, "printer.example.com", true)]
	public void GivenSharedCredentialsOrNoneWhenCertificateTrustCreatedThenTheSystemsTrustIsEnoughOnlyForConfiguredHostsWithThem
	(
		bool   hasSharedCredentials,
		string host,
		bool   expected
	)
	{
		// Arrange — one printer configured by URI, another with its own credentials
		IppNetworkOptions options = new IppNetworkOptions
		{
			Credentials = hasSharedCredentials ? new System.Net.NetworkCredential("ann", "secret") : null
		};

		options.PrinterUris.Add(new Uri("ipps://192.168.1.20/ipp/print"));
		options.PrinterCredentials[new Uri("ipps://192.168.1.21/ipp/print")] = new System.Net.NetworkCredential("bob", "secret");

		using ECDsa key = ECDsa.Create();
		using X509Certificate2 certificate = new CertificateRequest("CN=" + host, key, HashAlgorithmName.SHA256)
			.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

		// Act
		IppCertificateTrust trust = IppNetworkPrintSystemFactory.CreateCertificateTrust(options);
		bool trusted = trust.Validate(new object(), host, certificate, null, SslPolicyErrors.None);

		// Assert
		Assert.Equal(expected, trusted);
	}
}
