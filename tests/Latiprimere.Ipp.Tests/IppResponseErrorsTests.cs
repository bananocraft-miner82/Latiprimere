using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppResponseErrorsTests
{
	[Theory]
	[InlineData(typeof(FormatException))]
	[InlineData(typeof(UriFormatException))]
	[InlineData(typeof(OverflowException))]
	[InlineData(typeof(InvalidCastException))]
	public void GivenFailureFromUnreadableDataWhenCheckedThenIsUnreadable(Type exceptionType)
	{
		// Arrange — exceptionType is what mapping odd data can throw (provided by InlineData above)
		Exception exception = (Exception)Activator.CreateInstance(exceptionType)!;

		// Act
		bool isUnreadable = IppResponseErrors.IsUnreadableData(exception);

		// Assert
		Assert.True(isUnreadable);
	}

	[Theory]
	[InlineData(typeof(ArgumentException))]
	[InlineData(typeof(NullReferenceException))]
	[InlineData(typeof(OperationCanceledException))]
	public void GivenProgrammingErrorOrCancellationWhenCheckedThenIsNotUnreadable(Type exceptionType)
	{
		// Arrange — these must still propagate unchanged, as ADR 0008 requires (provided by InlineData above)
		Exception exception = (Exception)Activator.CreateInstance(exceptionType)!;

		// Act
		bool isUnreadable = IppResponseErrors.IsUnreadableData(exception);

		// Assert
		Assert.False(isUnreadable);
	}

	[Fact]
	public void GivenUnreadableAnswerWhenCreatedThenIsPrintExceptionNamingTheSourceAndKeepingTheCause()
	{
		// Arrange
		Uri source = new Uri("ipp://printserver:631/printers/Office");
		OverflowException cause = new OverflowException();

		// Act
		PrintException exception = IppResponseErrors.CreateUnreadable(source, cause);

		// Assert — a PrintException, which the printer catalog isolates
		Assert.Contains("ipp://printserver:631/printers/Office", exception.Message, StringComparison.Ordinal);
		Assert.Same(cause, exception.InnerException);
	}
}
