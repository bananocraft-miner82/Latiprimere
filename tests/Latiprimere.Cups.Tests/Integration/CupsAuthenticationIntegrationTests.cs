using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Cups.Tests.Integration;

/// <summary>
/// End-to-end checks of authentication against the test container's Secure queue, whose policy needs a user name
/// and password, sent over an encrypted connection, to print (tools/virtual-printers/cupsd.conf).
/// </summary>
/// <remarks>
/// Run only when <see cref="IntegrationEnvironment.CupsServerVariable"/> names the container's server; the raw-bytes
/// check also needs <see cref="IntegrationEnvironment.CupsOutputVariable"/>. Skipped on a Mac's own CUPS
/// (<see cref="IntegrationEnvironment.CupsSetupVariable"/>).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("OS", "Any")]
public sealed class CupsAuthenticationIntegrationTests
{
	private const string SecureId = "cups:Secure"; // Saves job data to /output/Secure-<job-id>.prn, for users who sign in

	private const string TestUserName = "latiprimere";   // The container's test account (tools/virtual-printers/Dockerfile)
	private const string TestPassword = "test-password"; // Its password

	[Fact]
	public async Task GivenNoCredentialsWhenPrintedToSecureQueueThenThrowsPrintAuthenticationException()
	{
		// Arrange — looking the queue up needs no password; printing to it does
		await using CatalogFixture fixture = CupsAuthenticationIntegrationTests.CreateCatalog(credentials: null);
		IPrinter printer = await CupsAuthenticationIntegrationTests.GetSecurePrinterAsync(fixture);
		using MemoryStream data = new MemoryStream(CupsAuthenticationIntegrationTests.CreateRawData());

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — over plain HTTP, CUPS asks for encryption (426) before it asks for the password
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(submit);
		Assert.Contains("needs a user name and password", exception.Message, StringComparison.Ordinal);
		Assert.Equal(426, exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenCredentialsWhenPrintedToSecureQueueThenQueueReceivesTheSameBytes()
	{
		// Arrange
		string? outputDirectory = IntegrationEnvironment.CupsOutputDirectory;
		Assert.SkipWhen(outputDirectory is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.CupsOutputVariable));
		NetworkCredential credentials = CupsAuthenticationIntegrationTests.CreateTestCredentials();
		await using CatalogFixture fixture = CupsAuthenticationIntegrationTests.CreateCatalog(credentials);
		IPrinter printer = await CupsAuthenticationIntegrationTests.GetSecurePrinterAsync(fixture);
		byte[] expected = CupsAuthenticationIntegrationTests.CreateRawData();
		using MemoryStream data = new MemoryStream(expected);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		string outputFile = Path.Combine(outputDirectory!, $"Secure-{job.Id}.prn"); // Not null: skipped above otherwise
		byte[]? received = await OutputFileWaiter.WaitForContentAsync(outputFile, expected, TestContext.Current.CancellationToken);

		// Assert — the job's status was readable too, as the user who owns it
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.Equal(expected, received);
	}

	[Fact]
	public async Task GivenCredentialsWhenGetPrintersThenListsQueuesOverTheEncryptedConnection()
	{
		// Arrange — with credentials, every request goes over TLS, not only printing
		NetworkCredential credentials = CupsAuthenticationIntegrationTests.CreateTestCredentials();
		await using CatalogFixture fixture = CupsAuthenticationIntegrationTests.CreateCatalog(credentials);

		// Act
		IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Contains(printers, printer => printer.Id == CupsAuthenticationIntegrationTests.SecureId);
	}

	[Fact]
	public async Task GivenWrongPasswordWhenPrintedToSecureQueueThenThrowsPrintAuthenticationException()
	{
		// Arrange
		NetworkCredential credentials = new NetworkCredential(CupsAuthenticationIntegrationTests.TestUserName, "wrong-password");
		await using CatalogFixture fixture = CupsAuthenticationIntegrationTests.CreateCatalog(credentials);
		IPrinter printer = await CupsAuthenticationIntegrationTests.GetSecurePrinterAsync(fixture);
		using MemoryStream data = new MemoryStream(CupsAuthenticationIntegrationTests.CreateRawData());

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(submit);
		Assert.Contains("did not accept the user name and password", exception.Message, StringComparison.Ordinal);
		Assert.Equal(401, exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenCredentialsWithoutEncryptionWhenPrintedToSecureQueueThenThrowsPrintAuthenticationException()
	{
		// Arrange
		await using CatalogFixture fixture = CupsAuthenticationIntegrationTests.CreateCatalog
		(
			CupsAuthenticationIntegrationTests.CreateTestCredentials(),
			CupsEncryption.Never
		);

		IPrinter printer = await CupsAuthenticationIntegrationTests.GetSecurePrinterAsync(fixture);
		using MemoryStream data = new MemoryStream(CupsAuthenticationIntegrationTests.CreateRawData());

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(submit);
		Assert.Contains("encryption is turned off", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenStandardCertificateCheckWhenConnectingWithCredentialsThenThrowsPrintExceptionAboutTheCertificate()
	{
		// Arrange — the container's certificate is self-signed, so the standard check refuses it
		string? server = IntegrationEnvironment.CupsServer;
		Assert.SkipWhen(server is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.CupsServerVariable));
		CupsAuthenticationIntegrationTests.SkipUnlessContainer();

		CupsOptions options = new CupsOptions
		{
			Server      = server,
			Credentials = CupsAuthenticationIntegrationTests.CreateTestCredentials()
		};

		using IPrintSystem printSystem = CupsPrintSystemFactory.Create(options);

		// Act
		Func<Task> getPrinters = () => printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(getPrinters);
		Assert.Contains("encrypted connection", exception.Message, StringComparison.Ordinal);
		Assert.IsType<HttpRequestException>(exception.InnerException);
	}

	/// <summary>
	/// Creates a catalog over the container's CUPS server, or skips the test when none is configured.
	/// </summary>
	/// <param name="credentials">The credentials to sign in with, or <see langword="null"/> for none.</param>
	/// <param name="encryption">When to encrypt.</param>
	/// <returns>The catalog fixture. The caller disposes it.</returns>
	private static CatalogFixture CreateCatalog(NetworkCredential? credentials, CupsEncryption encryption = CupsEncryption.Automatic)
	{
		string? server = IntegrationEnvironment.CupsServer;
		Assert.SkipWhen(server is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.CupsServerVariable));
		CupsAuthenticationIntegrationTests.SkipUnlessContainer();

		return new CatalogFixture
		(
			services => services.AddLatiprimereCups
			(
				options =>
				{
					options.Server                              = server;
					options.Credentials                         = credentials;
					options.Encryption                          = encryption;
					options.ServerCertificateValidationCallback = CupsAuthenticationIntegrationTests.TrustTestContainerCertificate;
				}
			)
		);
	}

	/// <summary>
	/// Skips the test when the server is a Mac's own CUPS, which has no Secure queue. Its behaviour differs too: CUPS
	/// asks for encryption only from other machines, and the runner's tests connect locally.
	/// </summary>
	private static void SkipUnlessContainer()
	{
		Assert.SkipWhen
		(
			IntegrationEnvironment.CupsSetup != CupsTestSetup.Container,
			IntegrationEnvironment.GetContainerOnlySkipReason(CupsAuthenticationIntegrationTests.SecureId)
		);
	}

	/// <summary>
	/// Creates the credentials of the container's test account.
	/// </summary>
	/// <returns>The credentials.</returns>
	private static NetworkCredential CreateTestCredentials()
	{
		return new NetworkCredential(CupsAuthenticationIntegrationTests.TestUserName, CupsAuthenticationIntegrationTests.TestPassword);
	}

	/// <summary>
	/// Trusts the test container's certificate.
	/// </summary>
	/// <param name="sender">Not used.</param>
	/// <param name="certificate">The server's certificate.</param>
	/// <param name="chain">Not used.</param>
	/// <param name="sslPolicyErrors">Not used.</param>
	/// <returns><see langword="true"/> whenever the server presented a certificate.</returns>
	/// <remarks>
	/// CUPS creates a new self-signed certificate each time the container is built, so there is nothing to pin it
	/// to. An application should compare the certificate with the server's known one instead, for example by
	/// thumbprint.
	/// </remarks>
	private static bool TrustTestContainerCertificate
	(
		object sender,
		X509Certificate? certificate,
		X509Chain? chain,
		SslPolicyErrors sslPolicyErrors
	)
	{
		return certificate is not null;
	}

	/// <summary>
	/// Looks up the Secure queue through the catalog.
	/// </summary>
	/// <param name="fixture">The catalog fixture.</param>
	/// <returns>The printer.</returns>
	private static Task<IPrinter> GetSecurePrinterAsync(CatalogFixture fixture)
	{
		return fixture.Catalog.GetPrinterAsync(CupsAuthenticationIntegrationTests.SecureId, TestContext.Current.CancellationToken);
	}

	/// <summary>
	/// Creates raw job data that is different every time.
	/// </summary>
	/// <returns>The data.</returns>
	private static byte[] CreateRawData()
	{
		return Encoding.ASCII.GetBytes($"Latiprimere authentication integration test {Guid.NewGuid():N}\r\n");
	}
}
