using Latiprimere.Ipp.Mapping;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

public sealed class IppVendorAttributeMapperTests
{
	[Theory]
	[InlineData("ipp:number-up", "4", Tag.Integer, 4)]
	[InlineData("ipp:print-quality", "5", Tag.Enum, 5)]
	[InlineData("ipp:finishings", "4", Tag.Enum, 4)]
	[InlineData("ipp:page-ranges-x", "-2", Tag.Integer, -2)]
	public void GivenIntegerValueWhenToAttributesThenSendsIntegerOrEnum(string key, string value, Tag expectedTag, int expectedValue)
	{
		// Arrange — attributes IPP defines as enums take the enum tag (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { [key] = value };

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.NetworkPrefixes, new PrintJobRequest());

		// Assert
		IppAttribute attribute = Assert.Single(attributes.Job);
		Assert.Equal(expectedTag, attribute.Tag);
		Assert.Equal(expectedValue, attribute.Value);
	}

	[Theory]
	[InlineData("600dpi", 600, 600, ResolutionUnit.DotsPerInch)]
	[InlineData("600x1200dpi", 600, 1200, ResolutionUnit.DotsPerInch)]
	[InlineData("118dpcm", 118, 118, ResolutionUnit.DotsPerCm)]
	public void GivenResolutionValueWhenToAttributesThenSendsResolution(string value, int width, int height, ResolutionUnit units)
	{
		// Arrange — as printer-resolution lists them (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { ["ipp:printer-resolution"] = value };

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.NetworkPrefixes, new PrintJobRequest());

		// Assert
		IppAttribute attribute = Assert.Single(attributes.Job);
		Assert.Equal(Tag.Resolution, attribute.Tag);
		Resolution resolution = Assert.IsType<Resolution>(attribute.Value);
		Assert.Equal(width, resolution.Width);
		Assert.Equal(height, resolution.Height);
		Assert.Equal(units, resolution.Units);
	}

	[Theory]
	[InlineData("cups:resolution", "600dpi")]
	[InlineData("ipp:x-vendor-label", "300dpi")]
	public void GivenResolutionLikeValueForOtherAttributeWhenToAttributesThenSendsName(string key, string value)
	{
		// Arrange — CUPS reads its resolution option as text, so "600dpi" stays a name, as before (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { [key] = value };

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.CupsPrefixes, new PrintJobRequest());

		// Assert
		IppAttribute attribute = Assert.Single(attributes.Job);
		Assert.Equal(Tag.NameWithoutLanguage, attribute.Tag);
		Assert.Equal(value, attribute.Value);
	}

	[Theory]
	[InlineData("true", Tag.Boolean, true)]
	[InlineData("false", Tag.Boolean, false)]
	[InlineData("draft", Tag.Keyword, "draft")]
	[InlineData("photo-glossy_2.x", Tag.Keyword, "photo-glossy_2.x")]
	[InlineData("True", Tag.NameWithoutLanguage, "True")]
	[InlineData("Office, 3rd floor", null, null)]
	[InlineData("Ann's job", Tag.NameWithoutLanguage, "Ann's job")]
	public void GivenOtherValueWhenToAttributesThenInfersBooleanKeywordOrName(string value, Tag? expectedTag, object? expectedValue)
	{
		// Arrange — the value and what it should be sent as; a comma always separates values (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { ["ipp:job-test"] = value };

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.NetworkPrefixes, new PrintJobRequest());

		// Assert
		if (expectedTag is null)
		{
			Assert.Equal(2, attributes.Job.Count);
			return;
		}

		IppAttribute attribute = Assert.Single(attributes.Job);
		Assert.Equal(expectedTag, attribute.Tag);
		Assert.Equal(expectedValue, attribute.Value);
	}

	[Fact]
	public void GivenValuesSeparatedByCommasWhenToAttributesThenSendsEachAsAValueOfTheSameAttribute()
	{
		// Arrange — staple (4) and punch (5)
		Dictionary<string, string> options = new Dictionary<string, string> { ["ipp:finishings"] = "4,5" };

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.NetworkPrefixes, new PrintJobRequest());

		// Assert
		Assert.Equal(["finishings", "finishings"], attributes.Job.Select(attribute => attribute.Name));
		Assert.Equal([4, 5], attributes.Job.Select(attribute => attribute.Value));
		Assert.All(attributes.Job, attribute => Assert.Equal(Tag.Enum, attribute.Tag));
	}

	[Fact]
	public void GivenOperationAttributeWhenToAttributesThenPutsItInTheOperationGroup()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string>
		{
			["ipp:ipp-attribute-fidelity"] = "true",
			["ipp:print-quality"]          = "3"
		};

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.NetworkPrefixes, new PrintJobRequest());

		// Assert
		Assert.Equal("ipp-attribute-fidelity", Assert.Single(attributes.Operation).Name);
		Assert.Equal("print-quality", Assert.Single(attributes.Job).Name);
	}

	[Fact]
	public void GivenKeysForOtherPrintSystemsWhenToAttributesForNetworkPrinterThenIgnoresThem()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string>
		{
			["cups:fit-to-page"] = "true",
			["windows:devmode"]  = "AAAA"
		};

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.NetworkPrefixes, new PrintJobRequest());

		// Assert
		Assert.Same(IppVendorAttributes.None, attributes);
		Assert.True(attributes.IsEmpty);
	}

	[Fact]
	public void GivenCupsAndIppKeysWhenToAttributesForCupsThenSendsBoth()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string>
		{
			["cups:fit-to-page"]  = "true",
			["ipp:print-quality"] = "5",
			["windows:devmode"]   = "AAAA"
		};

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.CupsPrefixes, new PrintJobRequest());

		// Assert — in key order, whatever order the dictionary had
		Assert.Equal(["fit-to-page", "print-quality"], attributes.Job.Select(attribute => attribute.Name));
	}

	[Fact]
	public void GivenSameAttributeFromIppAndCupsKeysWhenToAttributesForCupsThenThrowsArgumentException()
	{
		// Arrange
		Dictionary<string, string> options = new Dictionary<string, string>
		{
			["cups:print-quality"] = "4",
			["ipp:print-quality"]  = "5"
		};

		// Act
		Action map = () => IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.CupsPrefixes, new PrintJobRequest());

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(map);
		Assert.Contains("'cups:print-quality' and 'ipp:print-quality'", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("ipp:copies")]
	[InlineData("ipp:sides")]
	[InlineData("ipp:media")]
	[InlineData("ipp:job-name")]
	[InlineData("ipp:printer-uri")]
	[InlineData("ipp:document-format")]
	[InlineData("ipp:requesting-user-name")]
	public void GivenKeyForAttributeTheRequestSendsWhenToAttributesThenThrowsArgumentException(string key)
	{
		// Arrange — a request sending copies, sides, media and a job name (key provided by InlineData above)
		PrintJobRequest request = new PrintJobRequest
		{
			OperationAttributes   = new PrintJobOperationAttributes { JobName = new StringWithLanguage("Invoice") },
			JobTemplateAttributes = new JobTemplateAttributes
			{
				Copies = new IppValue<int>(2),
				Sides  = new IppValue<Sides>(Sides.OneSided),
				Media  = new IppValue<Media>(new Media("iso_a4_210x297mm", true))
			}
		};

		Dictionary<string, string> options = new Dictionary<string, string> { [key] = "x" };

		// Act
		Action map = () => IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.NetworkPrefixes, request);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(map);
		Assert.Contains($"'{key}'", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenKeyForTypedOptionLeftAtItsDefaultWhenToAttributesThenSendsIt()
	{
		// Arrange — one copy isn't sent, so ipp:copies is the only copies attribute
		Dictionary<string, string> options = new Dictionary<string, string> { ["ipp:copies"] = "3" };

		// Act
		IppVendorAttributes attributes = IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.NetworkPrefixes, new PrintJobRequest());

		// Assert
		Assert.Equal(3, Assert.Single(attributes.Job).Value);
	}

	[Theory]
	[InlineData("ipp:Print-Quality")]
	[InlineData("ipp:print quality")]
	[InlineData("ipp:4up")]
	[InlineData("cups:-fit")]
	public void GivenNameThatIsNotAnIppKeywordWhenToAttributesThenThrowsArgumentException(string key)
	{
		// Arrange — names IPP can't have (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { [key] = "5" };

		// Act
		Action map = () => IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.CupsPrefixes, new PrintJobRequest());

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(map);
		Assert.Contains($"'{key}'", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("")]
	[InlineData("4,,5")]
	[InlineData("4,")]
	public void GivenEmptyValueWhenToAttributesThenThrowsArgumentException(string value)
	{
		// Arrange — an empty value, or an empty one among several (provided by InlineData above)
		Dictionary<string, string> options = new Dictionary<string, string> { ["ipp:finishings"] = value };

		// Act
		Action map = () => IppVendorAttributeMapper.ToAttributes(options, IppVendorAttributeMapper.NetworkPrefixes, new PrintJobRequest());

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(map);
		Assert.Contains("'ipp:finishings'", exception.Message, StringComparison.Ordinal);
	}
}
