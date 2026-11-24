using Latiprimere.Rendering;
using Xunit;

namespace Latiprimere.Abstractions.Tests.Rendering;

public sealed class RenderingOptionsTests
{
	[Fact]
	public void GivenDefaultRenderOptionsWhenReadThenEverythingIsSmoothedAsBefore()
	{
		// Arrange
		RasterRenderOptions options = RasterRenderOptions.Default;

		// Act
		(bool text, bool lines, bool images) = (options.SmoothText, options.SmoothLines, options.SmoothImages);

		// Assert
		Assert.Equal((true, true, true), (text, lines, images));
		Assert.Equal(new RasterRenderOptions(), options);
	}

	[Fact]
	public void GivenDefaultOpenOptionsWhenReadThenThereIsNoPassword()
	{
		// Arrange
		PdfOpenOptions options = PdfOpenOptions.Default;

		// Act
		string? password = options.Password;

		// Assert
		Assert.Null(password);
	}

	[Fact]
	public void GivenPasswordWhenOpenOptionsAreWrittenAsTextThenThePasswordIsNotShown()
	{
		// Arrange
		PdfOpenOptions options = new PdfOpenOptions { Password = "correct horse battery staple" };

		// Act
		string text = options.ToString();

		// Assert: logs and debuggers show only that there is one
		Assert.DoesNotContain("horse", text, StringComparison.Ordinal);
		Assert.Equal("PdfOpenOptions { Password = (set) }", text);
		Assert.Equal("PdfOpenOptions { Password = null }", PdfOpenOptions.Default.ToString());
	}
}
