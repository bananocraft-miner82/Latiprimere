using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrintExceptionTests
{
	[Fact]
	public void GivenNoArgumentsWhenCreatedThenHasDefaultMessageAndNoNativeErrorCode()
	{
		// Arrange
		PrintException exception;

		// Act
		exception = new PrintException();

		// Assert
		Assert.False(string.IsNullOrWhiteSpace(exception.Message));
		Assert.Null(exception.NativeErrorCode);
	}

	[Fact]
	public void GivenNativeErrorCodeWhenInitialisedThenKeepsIt()
	{
		// Arrange
		const int accessDeniedErrorCode = 5;

		// Act
		PrintException exception = new PrintException("The printer refused the job.")
		{
			NativeErrorCode = accessDeniedErrorCode
		};

		// Assert
		Assert.Equal(accessDeniedErrorCode, exception.NativeErrorCode);
	}

	[Fact]
	public void GivenInnerExceptionWhenCreatedThenKeepsMessageAndCause()
	{
		// Arrange
		IOException cause = new IOException("Connection reset.");

		// Act
		PrintException exception = new PrintException("Sending the job failed.", cause);

		// Assert
		Assert.Equal("Sending the job failed.", exception.Message);
		Assert.Same(cause, exception.InnerException);
	}
}
