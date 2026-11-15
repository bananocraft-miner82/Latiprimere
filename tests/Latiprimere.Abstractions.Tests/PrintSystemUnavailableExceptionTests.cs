using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrintSystemUnavailableExceptionTests
{
	[Fact]
	public void GivenNoArgumentsWhenCreatedThenHasDefaultMessage()
	{
		// Arrange
		PrintSystemUnavailableException exception;

		// Act
		exception = new PrintSystemUnavailableException();

		// Assert
		Assert.False(string.IsNullOrWhiteSpace(exception.Message));
	}

	[Fact]
	public void GivenCustomMessageWhenCreatedThenUsesIt()
	{
		// Arrange
		const string message = "CUPS is not running.";

		// Act
		PrintSystemUnavailableException exception = new PrintSystemUnavailableException(message);

		// Assert
		Assert.Equal(message, exception.Message);
	}
}
