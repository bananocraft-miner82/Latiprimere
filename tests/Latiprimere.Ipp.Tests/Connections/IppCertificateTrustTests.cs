using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Latiprimere.Ipp.Connections;
using Xunit;

namespace Latiprimere.Ipp.Tests.Connections;

public sealed class IppCertificateTrustTests
{
	[Theory]
	[InlineData("3a:7f:00:11:22:33:44:55:66:77:88:99:aa:bb:cc:dd:ee:ff:00:11:22:33:44:55:66:77:88:99:aa:bb:cc:dd")]
	[InlineData("3A7F00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDD")]
	[InlineData("3A 7F 00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF 00 11 22 33 44 55 66 77 88 99 AA BB CC DD")]
	public void GivenFingerprintWithOrWithoutSeparatorsWhenNormalisedThenUpperCaseDigitsOnly(string fingerprint)
	{
		// Arrange (provided by InlineData above)

		// Act
		string normalised = IppCertificateTrust.NormalizeFingerprint(fingerprint);

		// Assert
		Assert.Equal("3A7F00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDD", normalised);
	}

	[Theory]
	[InlineData("")]
	[InlineData("3A:7F")]
	[InlineData("ZZ7F00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDD")]
	[InlineData("3A7F00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEE")]
	public void GivenSomethingThatIsntASha256FingerprintWhenTrustCreatedThenThrows(string fingerprint)
	{
		// Arrange (provided by InlineData above)

		// Act
		Action create = () => _ = new IppCertificateTrust([fingerprint], callback: null);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(create);
		Assert.Contains("SHA-256", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenCertificateTheSystemTrustsWhenValidatedThenTrusted()
	{
		// Arrange
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();
		IppCertificateTrust trust = new IppCertificateTrust([], callback: null);

		// Act
		bool trusted = trust.Validate(new object(), certificate, null, SslPolicyErrors.None);

		// Assert
		Assert.True(trusted);
	}

	[Theory]
	[InlineData("printer.example.com")]
	[InlineData("PRINTER.example.com")]
	[InlineData("[fd00::20]")]
	public void GivenHostsTheSystemsTrustIsEnoughForWhenANamedHostsCertificateIsValidatedThenTrusted(string host)
	{
		// Arrange — as configured: case and IPv6 brackets don't matter
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();
		IppCertificateTrust trust = new IppCertificateTrust([], callback: null, systemTrustHosts: ["printer.example.com", "fd00::20"]);

		// Act
		bool trusted = trust.Validate(new object(), host, certificate, null, SslPolicyErrors.None);

		// Assert
		Assert.True(trusted);
	}

	[Fact]
	public void GivenHostsTheSystemsTrustIsEnoughForWhenAnotherHostsCertificateIsValidatedThenRefusedSayingTheSystemTrustsIt()
	{
		// Arrange — a printer announced at a host with a certificate from a public authority
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();
		IppCertificateTrust trust = new IppCertificateTrust([], callback: null, systemTrustHosts: ["printer.example.com"]);

		// Act
		bool trusted = trust.Validate(new object(), "evil.example.net", certificate, null, SslPolicyErrors.None);
		bool trustedWithoutHost = trust.Validate(new object(), null, certificate, null, SslPolicyErrors.None);

		// Assert
		Assert.False(trusted);
		Assert.False(trustedWithoutHost);
		RefusedCertificate? refused = trust.FindRefusal("evil.example.net");
		Assert.NotNull(refused);
		Assert.True(refused.IsTrustedBySystem);
		Assert.Equal(IppCertificateTrust.GetFingerprint(certificate), refused.Fingerprint);
	}

	[Fact]
	public void GivenHostsTheSystemsTrustIsEnoughForWhenAnotherHostsCertificateIsPinnedThenTrusted()
	{
		// Arrange
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();
		IppCertificateTrust trust = new IppCertificateTrust([IppCertificateTrust.GetFingerprint(certificate)], callback: null, systemTrustHosts: []);

		// Act
		bool trusted = trust.Validate(new object(), "192.168.1.20", certificate, null, SslPolicyErrors.None);

		// Assert
		Assert.True(trusted);
	}

	[Fact]
	public void GivenUntrustedCertificateWhenRefusedThenTheRefusalSaysTheSystemDoesntTrustIt()
	{
		// Arrange
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();
		IppCertificateTrust trust = new IppCertificateTrust([], callback: null);

		// Act
		bool trusted = trust.Validate(new object(), "[fd00::20]", certificate, null, SslPolicyErrors.RemoteCertificateChainErrors);

		// Assert
		Assert.False(trusted);
		RefusedCertificate? refused = trust.FindRefusal("fd00::20");
		Assert.NotNull(refused);
		Assert.False(refused.IsTrustedBySystem);
	}

	[Fact]
	public void GivenCertificateRefusedThenAcceptedWhenTheRefusalIsLookedUpThenItIsGone()
	{
		// Arrange — the application's check refuses it at first, then accepts it, as after the user said to trust it
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();
		bool accepts = false;
		IppCertificateTrust trust = new IppCertificateTrust([], (_, _, _, _) => accepts);
		_ = trust.Validate(new object(), "192.168.1.20", certificate, null, SslPolicyErrors.RemoteCertificateChainErrors);
		RefusedCertificate? refusedFirst = trust.FindRefusal("192.168.1.20");
		accepts = true;

		// Act
		bool trusted = trust.Validate(new object(), "192.168.1.20", certificate, null, SslPolicyErrors.RemoteCertificateChainErrors);

		// Assert — a later TLS failure there mustn't be blamed on the certificate refused before
		Assert.NotNull(refusedFirst);
		Assert.True(trusted);
		Assert.Null(trust.FindRefusal("192.168.1.20"));
	}

	[Fact]
	public void GivenPinnedCertificateWithAnotherNameWhenValidatedThenTrusted()
	{
		// Arrange — a printer's self-signed certificate, naming another host, pinned in any case
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();
		string fingerprint = IppCertificateTrust.GetFingerprint(certificate).ToLowerInvariant();
		IppCertificateTrust trust = new IppCertificateTrust([fingerprint], callback: null);

		// Act
		bool trusted = trust.Validate(new object(), certificate, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch);

		// Assert
		Assert.True(trusted);
		Assert.True(trust.TrustsBeyondSystem);
	}

	[Fact]
	public void GivenUnpinnedSelfSignedCertificateWhenValidatedThenRefused()
	{
		// Arrange
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();
		using X509Certificate2 other = IppCertificateTrustTests.CreateCertificate();
		IppCertificateTrust trust = new IppCertificateTrust([IppCertificateTrust.GetFingerprint(other)], callback: null);

		// Act
		bool trusted = trust.Validate(new object(), certificate, null, SslPolicyErrors.RemoteCertificateChainErrors);

		// Assert
		Assert.False(trusted);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void GivenCallbackWhenUnpinnedCertificateValidatedThenTheCallbackDecides(bool answer)
	{
		// Arrange (provided by InlineData above)
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();
		X509Certificate? seen = null;
		IppCertificateTrust trust = new IppCertificateTrust([], (sender, cert, chain, errors) => { seen = cert; return answer; });

		// Act
		bool trusted = trust.Validate(new object(), certificate, null, SslPolicyErrors.RemoteCertificateChainErrors);

		// Assert
		Assert.Equal(answer, trusted);
		Assert.Same(certificate, seen);
		Assert.True(trust.TrustsBeyondSystem);
	}

	[Fact]
	public void GivenNothingBeyondTheSystemWhenAskedThenItDoesntTrustBeyondIt()
	{
		// Arrange
		IppCertificateTrust trust = new IppCertificateTrust([], callback: null);

		// Act
		bool beyond = trust.TrustsBeyondSystem;

		// Assert
		Assert.False(beyond);
	}

	[Fact]
	public void GivenCertificateWhenFingerprintTakenThenColonSeparatedSha256()
	{
		// Arrange
		using X509Certificate2 certificate = IppCertificateTrustTests.CreateCertificate();

		// Act
		string fingerprint = IppCertificateTrust.GetFingerprint(certificate);

		// Assert
		Assert.Equal(Convert.ToHexString(SHA256.HashData(certificate.RawData)), fingerprint.Replace(":", string.Empty, StringComparison.Ordinal));
		Assert.Equal(32, fingerprint.Split(':').Length);
	}

	/// <summary>
	/// Creates a self-signed certificate, as printers have.
	/// </summary>
	/// <returns>The certificate.</returns>
	internal static X509Certificate2 CreateCertificate()
	{
		using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		CertificateRequest request = new CertificateRequest("CN=HP ENVY 5640", key, HashAlgorithmName.SHA256);

		return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
	}
}
