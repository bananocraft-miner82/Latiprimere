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
	public void GivenPdfWithoutPasswordWhenFromPdfThenThereIsNoPassword()
	{
		// Arrange
		using MemoryStream stream = new MemoryStream();

		// Act
		PrintContent content = PrintContent.FromPdf(stream);

		// Assert
		Assert.Null(content.Password);
	}

	[Fact]
	public void GivenPasswordWhenFromPdfThenFormatIsPdfAndStreamAndPasswordAreKept()
	{
		// Arrange
		using MemoryStream stream = new MemoryStream();

		// Act
		PrintContent content = PrintContent.FromPdf(stream, "test-password");

		// Assert
		Assert.Equal((DocumentFormat.Pdf, "test-password"), (content.Format, content.Password));
		Assert.Same(stream, content.Content);
	}

	[Fact]
	public void GivenNullPasswordWhenFromPdfThenThrowsArgumentNullException()
	{
		// Arrange
		using MemoryStream stream = new MemoryStream();
		string? password = null;

		// Act
		Func<PrintContent> create = () => PrintContent.FromPdf(stream, password!); // Deliberately null to test the guard

		// Assert
		ArgumentNullException exception = Assert.Throws<ArgumentNullException>(create);
		Assert.Equal("password", exception.ParamName);
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

	[Theory]
	[InlineData(DocumentFormat.Jpeg)]
	[InlineData(DocumentFormat.Png)]
	public void GivenReadableStreamWhenFromAnImageThenFormatIsThatImagesAndStreamIsKept(DocumentFormat format)
	{
		// Arrange (provided by InlineData above)
		using MemoryStream stream = new MemoryStream();

		// Act
		PrintContent content = format == DocumentFormat.Jpeg ? PrintContent.FromJpeg(stream) : PrintContent.FromPng(stream);

		// Assert
		Assert.Equal(format, content.Format);
		Assert.Same(stream, content.Content);
	}

	[Fact]
	public void GivenUnreadableStreamWhenFromAnImageThenThrowsArgumentException()
	{
		// Arrange
		using WriteOnlyStream stream = new WriteOnlyStream();

		// Act
		Func<PrintContent> fromJpeg = () => PrintContent.FromJpeg(stream);
		Func<PrintContent> fromPng = () => PrintContent.FromPng(stream);

		// Assert
		Assert.Equal("content", Assert.Throws<ArgumentException>(fromJpeg).ParamName);
		Assert.Equal("content", Assert.Throws<ArgumentException>(fromPng).ParamName);
	}

	[Fact]
	public void GivenAStringWhenFromTextThenFormatIsTextAndTheStreamHoldsItInUtf8WithoutAByteOrderMark()
	{
		// Arrange
		string text = "café €5";

		// Act
		PrintContent content = PrintContent.FromText(text);

		// Assert
		using MemoryStream read = new MemoryStream();
		content.Content.CopyTo(read);
		Assert.Equal(DocumentFormat.Text, content.Format);
		Assert.Equal(System.Text.Encoding.UTF8.GetBytes(text), read.ToArray());
		Assert.Equal("utf-8", content.TextEncoding?.WebName);
	}

	[Fact]
	public void GivenAStreamAndEncodingWhenFromTextThenBothAreKept()
	{
		// Arrange
		using MemoryStream stream = new MemoryStream();

		// Act
		PrintContent content = PrintContent.FromText(stream, System.Text.Encoding.Latin1);

		// Assert
		Assert.Equal(DocumentFormat.Text, content.Format);
		Assert.Same(stream, content.Content);
		Assert.Same(System.Text.Encoding.Latin1, content.TextEncoding);
	}

	[Fact]
	public void GivenOtherFormatsWhenCreatedThenTheyHaveNoTextEncoding()
	{
		// Arrange
		using MemoryStream stream = new MemoryStream();

		// Act
		PrintContent pdf = PrintContent.FromPdf(stream);
		PrintContent raw = PrintContent.FromRaw(stream);

		// Assert
		Assert.Null(pdf.TextEncoding);
		Assert.Null(raw.TextEncoding);
	}

	[Fact]
	public void GivenNullTextOrEncodingWhenFromTextThenThrowsArgumentNullException()
	{
		// Arrange
		using MemoryStream stream = new MemoryStream();

		// Act
		Func<PrintContent> withoutText = () => PrintContent.FromText(null!); // Deliberately null to test the guard
		Func<PrintContent> withoutEncoding = () => PrintContent.FromText(stream, null!); // Likewise

		// Assert
		Assert.Throws<ArgumentNullException>(withoutText);
		Assert.Equal("encoding", Assert.Throws<ArgumentNullException>(withoutEncoding).ParamName);
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
