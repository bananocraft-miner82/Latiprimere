using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Latiprimere.Ipp.Connections;
using Xunit;

namespace Latiprimere.Ipp.Tests.Connections;

public sealed class BasicAuthenticationTests
{
	[Theory]
	[InlineData("", "no user name")]
	[InlineData("  ", "no user name")]
	[InlineData("ann:smith", "has a colon")]
	public void GivenCredentialsBasicAuthenticationCantCarryWhenValidatedThenThrowsTheCallersMessage(string userName, string expected)
	{
		// Arrange (provided by InlineData above)
		NetworkCredential credentials = new NetworkCredential(userName, "secret");

		// Act
		Action validate = () => BasicAuthentication.ValidateCredentials(credentials, "no user name", "has a colon");

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(validate);
		Assert.StartsWith(expected, exception.Message, StringComparison.Ordinal);
		Assert.Equal("credentials", exception.ParamName);
	}

	[Fact]
	public void GivenAUserNameWithoutAColonWhenValidatedThenNothingThrows()
	{
		// Arrange
		NetworkCredential credentials = new NetworkCredential("ann", "pass:word");

		// Act
		Exception? exception = Record.Exception(() => BasicAuthentication.ValidateCredentials(credentials, "no user name", "has a colon"));

		// Assert — a colon in the password is fine: only the first one separates
		Assert.Null(exception);
	}

	[Fact]
	public void GivenCredentialsWhenHeaderCreatedThenBasicWithUtf8UserAndPasswordInBase64()
	{
		// Arrange
		NetworkCredential credentials = new NetworkCredential("zoë", "pass:word");

		// Act
		AuthenticationHeaderValue? header = BasicAuthentication.CreateHeader(credentials);
		AuthenticationHeaderValue? none = BasicAuthentication.CreateHeader(null);

		// Assert
		Assert.NotNull(header);
		Assert.Equal("Basic", header.Scheme);
		Assert.Equal("zoë:pass:word", Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter!)));
		Assert.Null(none);
	}
}
