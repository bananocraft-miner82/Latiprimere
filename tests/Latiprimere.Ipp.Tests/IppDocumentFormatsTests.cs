using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppDocumentFormatsTests
{
	[Theory]
	[InlineData(DocumentFormat.Pdf, "application/pdf")]
	[InlineData(DocumentFormat.Raw, "application/vnd.cups-raw")]
	public void GivenCupsFormatsWhenGetMimeTypeThenReturnsCupsMimeType(DocumentFormat format, string expectedMimeType)
	{
		// Arrange — each document format and the MIME type CUPS expects (provided by InlineData above)
		IppDocumentFormats formats = IppDocumentFormats.Cups;

		// Act
		string mimeType = formats.GetMimeType(format);

		// Assert
		Assert.Equal(expectedMimeType, mimeType);
	}

	[Theory]
	[InlineData(DocumentFormat.Pdf, "application/pdf")]
	[InlineData(DocumentFormat.Raw, "application/octet-stream")]
	public void GivenStandardFormatsWhenGetMimeTypeThenReturnsIppMimeType
	(
		DocumentFormat format,
		string expectedMimeType
	)
	{
		// Arrange — each document format and the MIME type IPP printers expect (provided by InlineData above)
		IppDocumentFormats formats = IppDocumentFormats.Standard;

		// Act
		string mimeType = formats.GetMimeType(format);

		// Assert
		Assert.Equal(expectedMimeType, mimeType);
	}

	[Fact]
	public void GivenUnknownFormatWhenGetMimeTypeThenThrowsNotSupportedException()
	{
		// Arrange — a format a later version may add, as SubmitAsync documents for one it can't send (ADR 0044)
		DocumentFormat unknownFormat = (DocumentFormat)99;

		// Act
		Func<string> getMimeType = () => IppDocumentFormats.Cups.GetMimeType(unknownFormat);

		// Assert
		NotSupportedException exception = Assert.Throws<NotSupportedException>(getMimeType);
		Assert.Contains("99", exception.Message, StringComparison.Ordinal);
	}
}
