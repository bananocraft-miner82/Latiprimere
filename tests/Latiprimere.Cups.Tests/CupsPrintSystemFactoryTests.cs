using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Latiprimere.Ipp.Connections;
using Xunit;

namespace Latiprimere.Cups.Tests;

public sealed class CupsPrintSystemFactoryTests
{
	private const string InvalidEnvironmentServer = "printserver:notaport"; // A CUPS_SERVER value that isn't a server

	private static readonly CupsEndpoint TcpEndpoint = new CupsEndpoint // A server reached over TCP
	{
		ServerUri  = new Uri("ipp://printserver:631/"),
		IsExplicit = true
	};

	[Fact]
	public void GivenExplicitServerWhenCreateThenPrintSystemIsSupportedOnAnyPlatform()
	{
		// Arrange
		CupsOptions options = new CupsOptions { Server = "printserver:631" };

		// Act
		IPrintSystem printSystem = CupsPrintSystemFactory.Create(options);

		// Assert
		Assert.True(printSystem.IsSupported);
		Assert.IsAssignableFrom<IDisposable>(printSystem);
		((IDisposable)printSystem).Dispose();
	}

	[Theory]
	[InlineData(CupsEncryption.Automatic, false, false)]
	[InlineData(CupsEncryption.Automatic, true, true)]
	[InlineData(CupsEncryption.Always, false, true)]
	[InlineData(CupsEncryption.Always, true, true)]
	[InlineData(CupsEncryption.Never, false, false)]
	[InlineData(CupsEncryption.Never, true, false)]
	public void GivenTcpServerWhenConnectionSecurityCreatedThenUsesTlsAsEncryptionAndCredentialsSay
	(
		CupsEncryption encryption,
		bool hasCredentials,
		bool expectedTls
	)
	{
		// Arrange — the encryption setting, whether credentials are set, and whether TLS is expected (provided by
		// InlineData above)
		CupsOptions options = new CupsOptions
		{
			Encryption  = encryption,
			Credentials = hasCredentials ? new NetworkCredential("ann", "secret") : null
		};

		// Act
		IppConnectionSecurity security = CupsPrintSystemFactory.CreateConnectionSecurity(options, CupsPrintSystemFactoryTests.TcpEndpoint);

		// Assert
		Assert.Equal(expectedTls, security.UseTls);
		Assert.Same(options.Credentials, security.Credentials);
	}

	[Fact]
	public void GivenLocalSocketWhenConnectionSecurityCreatedThenNeverUsesTlsButKeepsCredentials()
	{
		// Arrange
		CupsOptions options = new CupsOptions
		{
			Encryption  = CupsEncryption.Always,
			Credentials = new NetworkCredential("ann", "secret")
		};

		CupsEndpoint endpoint = new CupsEndpoint
		{
			ServerUri  = new Uri("ipp://localhost:631/"),
			SocketPath = "/run/cups/cups.sock"
		};

		// Act
		IppConnectionSecurity security = CupsPrintSystemFactory.CreateConnectionSecurity(options, endpoint);

		// Assert
		Assert.False(security.UseTls);
		Assert.Same(options.Credentials, security.Credentials);
	}

	[Fact]
	public void GivenCertificateCheckWhenConnectionSecurityCreatedThenPassesItOn()
	{
		// Arrange
		CupsOptions options = new CupsOptions
		{
			Encryption                          = CupsEncryption.Always,
			ServerCertificateValidationCallback = CupsPrintSystemFactoryTests.RejectEveryCertificate
		};

		// Act
		IppConnectionSecurity security = CupsPrintSystemFactory.CreateConnectionSecurity(options, CupsPrintSystemFactoryTests.TcpEndpoint);

		// Assert
		Assert.Same(options.ServerCertificateValidationCallback, security.ServerCertificateValidationCallback);
	}

	[Theory]
	[InlineData("")]
	[InlineData("  ")]
	[InlineData("domain:ann")]
	public void GivenCredentialsWithUnusableUserNameWhenCreateThenThrowsArgumentException(string userName)
	{
		// Arrange — userName is blank, or holds the colon Basic authentication separates on (provided by InlineData
		// above)
		CupsOptions options = new CupsOptions
		{
			Server      = "printserver:631",
			Credentials = new NetworkCredential(userName, "secret")
		};

		// Act
		Func<IPrintSystem> create = () => CupsPrintSystemFactory.Create(options);

		// Assert
		Assert.Throws<ArgumentException>(create);
	}

	[Fact]
	public void GivenCredentialsWithoutUserNameOptionWhenRequestingUserNameResolvedThenUsesTheCredentialsUser()
	{
		// Arrange — CUPS uses the authenticated user once a request is authenticated, so the two should match
		CupsOptions options = new CupsOptions { Credentials = new NetworkCredential("ann", "secret") };

		// Act
		string userName = CupsPrintSystemFactory.GetRequestingUserName(options);

		// Assert
		Assert.Equal("ann", userName);
	}

	[Fact]
	public void GivenUserNameAndCredentialsWhenRequestingUserNameResolvedThenUserNameWins()
	{
		// Arrange
		CupsOptions options = new CupsOptions
		{
			UserName    = "labels",
			Credentials = new NetworkCredential("ann", "secret")
		};

		// Act
		string userName = CupsPrintSystemFactory.GetRequestingUserName(options);

		// Assert
		Assert.Equal("labels", userName);
	}

	[Fact]
	public void GivenInvalidServerWhenCreateThenThrowsArgumentException()
	{
		// Arrange
		CupsOptions options = new CupsOptions { Server = "printserver:notaport" };

		// Act
		Func<IPrintSystem> create = () => CupsPrintSystemFactory.Create(options);

		// Assert
		Assert.Throws<ArgumentException>(create);
	}

	[Fact]
	public void GivenInvalidEnvironmentServerWhenCreateThenReturnsSupportedPrintSystemInsteadOfThrowing()
	{
		// Arrange — CUPS_SERVER comes from the user's environment, so it mustn't stop the application starting
		CupsOptions options = new CupsOptions();

		// Act
		using IPrintSystem printSystem = CupsPrintSystemFactory.Create(options, CupsPrintSystemFactoryTests.InvalidEnvironmentServer);

		// Assert — supported, so the catalog asks it and reports why it fails, rather than silently skipping it
		Assert.True(printSystem.IsSupported);
	}

	[Theory]
	[InlineData("list")]
	[InlineData("find")]
	[InlineData("default")]
	public async Task GivenInvalidEnvironmentServerWhenPrintSystemUsedThenThrowsPrintExceptionNamingTheVariable(string operation)
	{
		// Arrange — operation is the print system call to make (provided by InlineData above)
		using IPrintSystem printSystem = CupsPrintSystemFactory.Create
		(
			new CupsOptions(),
			CupsPrintSystemFactoryTests.InvalidEnvironmentServer
		);

		// Act
		Func<Task> use = () => CupsPrintSystemFactoryTests.UseAsync(printSystem, operation);

		// Assert — a PrintException, so the catalog skips CUPS and still lists the other print systems (ADR 0008)
		PrintException exception = await Assert.ThrowsAsync<PrintException>(use);
		Assert.Contains("CUPS_SERVER", exception.Message, StringComparison.Ordinal);
		Assert.Contains(CupsPrintSystemFactoryTests.InvalidEnvironmentServer, exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Makes one call of a print system.
	/// </summary>
	/// <param name="printSystem">The print system.</param>
	/// <param name="operation"><c>list</c>, <c>find</c> or <c>default</c>.</param>
	/// <returns>A task that completes when the call has.</returns>
	private static async Task UseAsync(IPrintSystem printSystem, string operation)
	{
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;

		switch (operation)
		{
			case "list":
				await printSystem.GetPrintersAsync(cancellationToken);
				break;

			case "find":
				await printSystem.FindPrinterAsync("cups:Office", cancellationToken);
				break;

			default:
				await printSystem.FindDefaultPrinterAsync(cancellationToken);
				break;
		}
	}

	/// <summary>
	/// A certificate check that trusts nothing, to see that it's passed on.
	/// </summary>
	/// <param name="sender">Not used.</param>
	/// <param name="certificate">Not used.</param>
	/// <param name="chain">Not used.</param>
	/// <param name="sslPolicyErrors">Not used.</param>
	/// <returns>Always <see langword="false"/>.</returns>
	private static bool RejectEveryCertificate
	(
		object sender,
		X509Certificate? certificate,
		X509Chain? chain,
		SslPolicyErrors sslPolicyErrors
	)
	{
		return false;
	}
}
