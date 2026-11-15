using Latiprimere.Abstractions.Tests.Fakes;
using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrintContentTests
{
	[Fact]
	public void GivenReadableStreamWhenFromPdfThenFormatIsPdfAndStreamIsKept()
	{
		// Arrange
		using MemoryStream stream = new MemoryStream();

		// Act
		PrintContent content = PrintContent.FromPdf(stream);

		// Assert
		Assert.Equal(DocumentFormat.Pdf, content.Format);
		Assert.Same(stream, content.Content);
	}

	[Fact]
	public void GivenReadableStreamWhenFromRawThenFormatIsRawAndStreamIsKept()
	{
		// Arrange
		using MemoryStream stream = new MemoryStream();

		// Act
		PrintContent content = PrintContent.FromRaw(stream);

		// Assert
		Assert.Equal(DocumentFormat.Raw, content.Format);
		Assert.Same(stream, content.Content);
	}

	[Fact]
	public void GivenNullStreamWhenFromPdfThenThrowsArgumentNullException()
	{
		// Arrange
		Stream? stream = null;

		// Act
		Func<PrintContent> createContent = () => PrintContent.FromPdf(stream!); // Deliberately null to test the guard

		// Assert
		Assert.Throws<ArgumentNullException>(createContent);
	}

	[Fact]
	public void GivenUnreadableStreamWhenFromRawThenThrowsArgumentException()
	{
		// Arrange
		using WriteOnlyStream stream = new WriteOnlyStream();

		// Act
		Func<PrintContent> createContent = () => PrintContent.FromRaw(stream);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(createContent);
		Assert.Equal("content", exception.ParamName);
		Assert.False(string.IsNullOrWhiteSpace(exception.Message));
	}
}
