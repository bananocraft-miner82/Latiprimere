using Xunit;

namespace Latiprimere.Cups.Tests;

public sealed class CupsOptionsTests
{
	[Fact]
	public void GivenNewOptionsWhenReadThenConnectTimeoutIsTenSeconds()
	{
		// Arrange
		CupsOptions options = new CupsOptions();

		// Act
		TimeSpan connectTimeout = options.ConnectTimeout;

		// Assert
		Assert.Equal(TimeSpan.FromSeconds(10), connectTimeout);
	}

	[Fact]
	public void GivenPositiveDurationWhenConnectTimeoutSetThenKeepsIt()
	{
		// Arrange
		CupsOptions options = new CupsOptions();

		// Act
		options.ConnectTimeout = TimeSpan.FromSeconds(3);

		// Assert
		Assert.Equal(TimeSpan.FromSeconds(3), options.ConnectTimeout);
	}

	[Fact]
	public void GivenNewOptionsWhenReadThenNoCredentialsAndAutomaticEncryption()
	{
		// Arrange
		CupsOptions options = new CupsOptions();

		// Act
		CupsEncryption encryption = options.Encryption;

		// Assert
		Assert.Equal(CupsEncryption.Automatic, encryption);
		Assert.Null(options.Credentials);
		Assert.Null(options.ServerCertificateValidationCallback);
	}

	[Fact]
	public void GivenUndefinedValueWhenEncryptionSetThenThrowsArgumentOutOfRangeException()
	{
		// Arrange
		CupsOptions options = new CupsOptions();

		// Act
		Action set = () => options.Encryption = (CupsEncryption)99;

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(set);
	}

	[Fact]
	public void GivenNegativeDurationWhenConnectTimeoutSetThenThrowsArgumentOutOfRangeException()
	{
		// Arrange
		CupsOptions options = new CupsOptions();

		// Act
		Action set = () => options.ConnectTimeout = TimeSpan.FromSeconds(-1);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(set);
	}
}
