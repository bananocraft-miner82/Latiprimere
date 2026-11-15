using Latiprimere.Ipp.Mapping;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

public sealed class IppValueReaderTests
{
	[Fact]
	public void GivenTextValueWhenReadStringThenReturnsText()
	{
		// Arrange
		StringWithLanguage? value = new StringWithLanguage("Office");

		// Act
		string? text = IppValueReader.ReadString(value);

		// Assert
		Assert.Equal("Office", text);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GivenMissingOrBlankTextWhenReadStringThenReturnsNull(string? rawValue)
	{
		// Arrange — rawValue is absent, empty or whitespace (provided by InlineData above)
		StringWithLanguage? value = rawValue is null ? null : new StringWithLanguage(rawValue);

		// Act
		string? text = IppValueReader.ReadString(value);

		// Assert
		Assert.Null(text);
	}

	[Fact]
	public void GivenPresentValueWhenReadValueThenReturnsIt()
	{
		// Arrange
		IppValue<int>? value = new IppValue<int>(3);

		// Act
		int? number = IppValueReader.ReadValue(value);

		// Assert
		Assert.Equal(3, number);
	}

	[Fact]
	public void GivenNoValueWhenReadValueThenReturnsNull()
	{
		// Arrange
		IppValue<int>? value = IppValue<int>.NoValue;

		// Act
		int? number = IppValueReader.ReadValue(value);

		// Assert
		Assert.Null(number);
	}

	[Fact]
	public void GivenAbsentValueWhenReadReferenceThenReturnsNull()
	{
		// Arrange
		IppValue<Uri>? value = null;

		// Act
		Uri? uri = IppValueReader.ReadReference(value);

		// Assert
		Assert.Null(uri);
	}

	[Fact]
	public void GivenPresentReferenceWhenReadReferenceThenReturnsIt()
	{
		// Arrange
		Uri expectedUri = new Uri("ipp://localhost/printers/Office");
		IppValue<Uri>? value = new IppValue<Uri>(expectedUri);

		// Act
		Uri? uri = IppValueReader.ReadReference(value);

		// Assert
		Assert.Same(expectedUri, uri);
	}
}
