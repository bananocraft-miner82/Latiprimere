using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// Makes the self-signed certificate an encrypted fake printer presents, as most printers do.
/// </summary>
internal static class SelfSignedCertificate
{
	/// <summary>
	/// Creates a certificate for <c>localhost</c>, <c>127.0.0.1</c> and <c>::1</c>, valid for a week.
	/// </summary>
	/// <returns>The certificate, with its private key. The caller disposes it.</returns>
	public static X509Certificate2 Create()
	{
		using RSA key = RSA.Create(2048);
		CertificateRequest request = new CertificateRequest("CN=Latiprimere fake printer", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

		SubjectAlternativeNameBuilder names = new SubjectAlternativeNameBuilder();
		names.AddDnsName("localhost");
		names.AddIpAddress(IPAddress.Loopback);
		names.AddIpAddress(IPAddress.IPv6Loopback);
		request.CertificateExtensions.Add(names.Build());
		request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false)); // Server authentication

		DateTimeOffset now = DateTimeOffset.UtcNow;
		using X509Certificate2 ephemeral = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(7));

		// On Windows, TLS can't use a key that exists only in memory, so the certificate goes through PKCS #12
		return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
	}
}
