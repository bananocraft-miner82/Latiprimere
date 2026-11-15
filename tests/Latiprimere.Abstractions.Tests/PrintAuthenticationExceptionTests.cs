using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrintAuthenticationExceptionTests
{
	[Fact]
	public void GivenNoArgumentsWhenCreatedThenHasDefaultMessage()
	{
		// Arrange
		PrintAuthenticationException exception;

		// Act
		exception = new PrintAuthenticationException();

		// Assert
		Assert.False(string.IsNullOrWhiteSpace(exception.Message));
	}

	[Fact]
	public void GivenMessageAndCauseWhenCreatedThenIsAPrintExceptionKeepingBoth()
	{
		// Arrange
		const string message = "The print server needs a user name and password.";
		HttpRequestException cause = new HttpRequestException("Unauthorized");

		// Act
		PrintException exception = new PrintAuthenticationException(message, cause) { NativeErrorCode = 401 };

		// Assert — callers that only catch PrintException still catch it
		Assert.IsType<PrintAuthenticationException>(exception);
		Assert.Equal(message, exception.Message);
		Assert.Same(cause, exception.InnerException);
		Assert.Equal(401, exception.NativeErrorCode);
	}
}
