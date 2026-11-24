using Latiprimere.Rendering.Pdfium.Interop;
using Latiprimere.Rendering.Pdfium.Tests.Fakes;
using Latiprimere.Testing.Fakes;
using Latiprimere.Testing.Fixtures;
using Xunit;

namespace Latiprimere.Rendering.Pdfium.Tests;

public sealed class PdfiumRasterizerTests
{
	private const int StartingBufferBytes = 64 * 1024; // The rasterizer's first buffer for a stream that can't seek

	[Fact]
	public void GivenOnePagePdfWhenOpenThenReportsOneA4Page()
	{
		// Arrange
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		using FileStream content = TestDocuments.Open(TestDocuments.MixedOnePage);

		// Act
		using IRasterDocument document = rasterizer.Open(content);

		// Assert
		Assert.Equal(1, document.PageCount);
		PageSize size = document.GetPageSize(0);
		Assert.Equal(595, size.WidthPoints, 0.5);
		Assert.Equal(842, size.HeightPoints, 0.5);
		Assert.False(size.IsLandscape);
	}

	[Fact]
	public void GivenHundredPagePdfWhenOpenThenReportsHundredPages()
	{
		// Arrange
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		using FileStream content = TestDocuments.Open(TestDocuments.MixedHundredPages);

		// Act
		using IRasterDocument document = rasterizer.Open(content);

		// Assert
		Assert.Equal(100, document.PageCount);
	}

	[Fact]
	public void GivenPdfFromStreamThatCantSeekWhenOpenThenReadsItAll()
	{
		// Arrange — no length to size the buffer by, so it has to grow; the hundred pages are larger than its start
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		using MemoryStream copy = new MemoryStream();

		using (FileStream file = TestDocuments.Open(TestDocuments.MixedHundredPages))
		{
			file.CopyTo(copy);
		}

		using ForwardOnlyStream content = new ForwardOnlyStream(copy.ToArray());

		// Act
		using IRasterDocument document = rasterizer.Open(content);

		// Assert
		Assert.Equal(100, document.PageCount);
	}

	[Fact]
	public void GivenStreamLargerThanTwoGigabytesWhenOpenThenThrowsPrintExceptionWithoutReadingIt()
	{
		// Arrange — the stream claims more than 2 GB but holds nothing, so it must be refused from its length
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		using OversizedStream content = new OversizedStream();

		// Act
		Func<IRasterDocument> open = () => rasterizer.Open(content);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(open);
		Assert.Contains("larger than", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenStreamThatCantSeekExactlyAtTheLimitWhenOpenThenOpensIt()
	{
		// Arrange — padded after %%EOF to fill the starting buffer exactly, which is then full at the limit
		byte[] pdf = PdfiumRasterizerTests.ReadPaddedOnePagePdf(PdfiumRasterizerTests.StartingBufferBytes);
		using ForwardOnlyStream content = new ForwardOnlyStream(pdf);

		// Act
		using IRasterDocument document = PdfiumRasterizer.Open(content, maxDocumentBytes: pdf.Length);

		// Assert
		Assert.Equal(1, document.PageCount);
	}

	[Theory]
	[InlineData(PdfiumRasterizerTests.StartingBufferBytes)]
	[InlineData(PdfiumRasterizerTests.StartingBufferBytes + 10)]
	[InlineData(100)]
	public void GivenStreamThatCantSeekOneByteOverTheLimitWhenOpenThenThrowsPrintException(int maxDocumentBytes)
	{
		// Arrange — the limit is the starting buffer's size, a little more, so the buffer grows to just past it, or
		// less, so the whole document fits in the buffer (provided by InlineData above)
		byte[] pdf = PdfiumRasterizerTests.ReadPaddedOnePagePdf(maxDocumentBytes + 1);
		using ForwardOnlyStream content = new ForwardOnlyStream(pdf);

		// Act
		Func<IRasterDocument> open = () => PdfiumRasterizer.Open(content, maxDocumentBytes);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(open);
		Assert.Contains("larger than", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenAMaximumSizeInTheOptionsWhenALargerPdfIsOpenedThenThrowsPrintExceptionWithoutReadingIt()
	{
		// Arrange — the application's own limit, one byte short of the document
		using FileStream content = TestDocuments.Open(TestDocuments.MixedOnePage);
		PdfiumRasterizer rasterizer = new PdfiumRasterizer(new PdfiumOptions { MaxDocumentBytes = (int)content.Length - 1 });

		// Act
		Func<IRasterDocument> open = () => rasterizer.Open(content);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(open);
		Assert.Contains("larger than", exception.Message, StringComparison.Ordinal);
		Assert.Equal(0, content.Position);
	}

	[Fact]
	public void GivenAMaximumSizeInTheOptionsWhenAPdfWithinItIsOpenedThenItOpens()
	{
		// Arrange
		using FileStream content = TestDocuments.Open(TestDocuments.MixedOnePage);
		PdfiumRasterizer rasterizer = new PdfiumRasterizer(new PdfiumOptions { MaxDocumentBytes = (int)content.Length });

		// Act
		using IRasterDocument document = rasterizer.Open(content);

		// Assert
		Assert.Equal(1, document.PageCount);
	}

	[Fact]
	public void GivenOptionsChangedAfterTheRasteriserIsMadeWhenAPdfIsOpenedThenTheOriginalLimitHolds()
	{
		// Arrange
		using FileStream content = TestDocuments.Open(TestDocuments.MixedOnePage);
		PdfiumOptions options = new PdfiumOptions();
		PdfiumRasterizer rasterizer = new PdfiumRasterizer(options);

		// Act
		options.MaxDocumentBytes = 1;
		using IRasterDocument document = rasterizer.Open(content);

		// Assert
		Assert.Equal(1, document.PageCount);
	}

	[Fact]
	public void GivenNullOptionsWhenRasteriserIsMadeThenThrowsArgumentNullException()
	{
		// Arrange
		PdfiumOptions? options = null;

		// Act
		Action create = () => _ = new PdfiumRasterizer(options!);

		// Assert
		Assert.Throws<ArgumentNullException>(create);
	}

	[Fact]
	public void GivenSeekableStreamOverTheLimitWhenOpenThenThrowsPrintExceptionWithoutReadingIt()
	{
		// Arrange
		using FileStream content = TestDocuments.Open(TestDocuments.MixedOnePage);

		// Act
		Func<IRasterDocument> open = () => PdfiumRasterizer.Open(content, maxDocumentBytes: (int)content.Length - 1);

		// Assert — refused from its length
		Assert.Throws<PrintException>(open);
		Assert.Equal(0, content.Position);
	}

	[Fact]
	public void GivenStreamThatFailsPartwayWhenOpenThenTheFailurePropagates()
	{
		// Arrange — the stream fails after more data than the starting buffer holds, so the buffer has already grown
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		byte[] pdf = PdfiumRasterizerTests.ReadPaddedOnePagePdf(PdfiumRasterizerTests.StartingBufferBytes * 2);
		using FailingStream content = new FailingStream(pdf);

		// Act
		Func<IRasterDocument> open = () => rasterizer.Open(content);

		// Assert — the caller's stream failed, which is the caller's to handle, not a printing failure
		Assert.Throws<IOException>(open);
	}

	[Theory]
	[InlineData(TestDocuments.LineArtPassword)]
	[InlineData("test-owner")]
	public void GivenPasswordProtectedPdfWithItsPasswordWhenOpenThenItRenders(string password)
	{
		// Arrange: either the password for opening it or the owner password
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		using FileStream content = TestDocuments.Open(TestDocuments.LineArtWithPassword);
		RasterBand band = RasterBand.Create(595, 842, 0, 842, RasterPixelFormat.Gray8);
		byte[] pixels = new byte[band.ByteCount];

		// Act
		using IRasterDocument document = rasterizer.Open(content, new PdfOpenOptions { Password = password });
		document.RenderBand(0, band, pixels);

		// Assert: the middle of the circle, at (300, 642) from the top, is black
		Assert.Equal(1, document.PageCount);
		Assert.Equal(0, pixels[(642 * 595) + 300]);
	}

	[Fact]
	public void GivenPasswordProtectedPdfWithoutPasswordWhenOpenThenThrowsPrintExceptionSayingHowToGiveIt()
	{
		// Arrange
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		using FileStream content = TestDocuments.Open(TestDocuments.LineArtWithPassword);

		// Act
		Func<IRasterDocument> open = () => rasterizer.Open(content);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(open);
		Assert.Contains("PrintContent.FromPdf(stream, password)", exception.Message, StringComparison.Ordinal);
		Assert.Equal((int)PdfiumConstants.ErrorPassword, exception.NativeErrorCode);
	}

	[Fact]
	public void GivenWrongPasswordWhenOpenThenThrowsPrintExceptionWithoutThePassword()
	{
		// Arrange
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		using FileStream content = TestDocuments.Open(TestDocuments.LineArtWithPassword);
		PdfOpenOptions options = new PdfOpenOptions { Password = "not-the-password" };

		// Act
		Func<IRasterDocument> open = () => rasterizer.Open(content, options);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(open);
		Assert.Contains("open it", exception.Message, StringComparison.Ordinal);
		Assert.DoesNotContain("not-the-password", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenBytesThatAreNotPdfWhenOpenThenThrowsPrintExceptionWithPdfiumError()
	{
		// Arrange
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		using MemoryStream content = new MemoryStream("This is not a PDF."u8.ToArray());

		// Act
		Func<IRasterDocument> open = () => rasterizer.Open(content);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(open);
		Assert.NotNull(exception.NativeErrorCode);
	}

	[Fact]
	public void GivenNullContentWhenOpenThenThrowsArgumentNullException()
	{
		// Arrange
		PdfiumRasterizer rasterizer = new PdfiumRasterizer();
		Stream? content = null;

		// Act
		Func<IRasterDocument> open = () => rasterizer.Open(content!); // Deliberately null to test the guard

		// Assert
		Assert.Throws<ArgumentNullException>(open);
	}

	/// <summary>
	/// Reads the one-page test PDF, padded with line breaks after its end to the given length if it is shorter.
	/// </summary>
	/// <param name="length">The length wanted, in bytes.</param>
	/// <returns>The document.</returns>
	private static byte[] ReadPaddedOnePagePdf(int length)
	{
		using MemoryStream copy = new MemoryStream();

		using (FileStream file = TestDocuments.Open(TestDocuments.MixedOnePage))
		{
			file.CopyTo(copy);
		}

		while (copy.Length < length)
		{
			copy.WriteByte((byte)'\n');
		}

		return copy.ToArray();
	}
}
