using System.Text;
using System.Xml;
using System.Xml.Linq;
using Latiprimere.Windows.PrintTickets;
using Xunit;

namespace Latiprimere.Windows.Tests.PrintTickets;

public sealed class PrintCapabilitiesDocumentTests
{
	private const string Printer = "Office Inkjet";

	/// <summary>
	/// Modelled on what the HP class driver and XPS Document Writer report: a vendor namespace with a version in it,
	/// display names on some elements only, options told apart by scored properties alone, options the device or an
	/// administrator rules out, features the typed options cover, and number, text and blob parameters.
	/// </summary>
	internal const string Capabilities = """
		<?xml version="1.0" encoding="UTF-8"?>
		<psf:PrintCapabilities xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" version="1" xmlns:ns0000="http://schemas.microsoft.com/windows/printing/oemdriverpt/Office Inkjet/10.0.1/" xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
		  <psf:ParameterDef name="ns0000:PageDevmodeSnapshot">
		    <psf:Property name="psf:DataType"><psf:Value xsi:type="xsd:QName">xsd:string</psf:Value></psf:Property>
		    <psf:Property name="psf:UnitType"><psf:Value xsi:type="xsd:string">base64</psf:Value></psf:Property>
		  </psf:ParameterDef>
		  <psf:ParameterDef name="psk:JobCopiesAllDocuments">
		    <psf:Property name="psf:DataType"><psf:Value xsi:type="xsd:QName">xsd:integer</psf:Value></psf:Property>
		    <psf:Property name="psf:MinValue"><psf:Value xsi:type="xsd:integer">1</psf:Value></psf:Property>
		    <psf:Property name="psf:MaxValue"><psf:Value xsi:type="xsd:integer">999</psf:Value></psf:Property>
		  </psf:ParameterDef>
		  <psf:Feature name="psk:PageMediaSize">
		    <psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">Paper Size</psf:Value></psf:Property>
		    <psf:Option name="psk:ISOA4"><psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">A4</psf:Value></psf:Property></psf:Option>
		  </psf:Feature>
		  <psf:Feature name="psk:PageOrientation">
		    <psf:Option name="psk:Portrait" />
		  </psf:Feature>
		  <psf:Feature name="psk:PageOutputQuality">
		    <psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">Output Quality</psf:Value></psf:Property>
		    <psf:Option name="psk:High" constrained="psk:None"><psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">Best</psf:Value></psf:Property></psf:Option>
		    <psf:Option name="psk:Normal" constrained="psk:None"><psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">Normal</psf:Value></psf:Property></psf:Option>
		    <psf:Option name="psk:Draft" constrained="psk:AdminSettings"><psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">Fast Draft</psf:Value></psf:Property></psf:Option>
		  </psf:Feature>
		  <psf:Feature name="psk:DocumentNUp">
		    <psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">Pages per sheet</psf:Value></psf:Property>
		    <psf:Option constrained="psk:None">
		      <psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">1</psf:Value></psf:Property>
		      <psf:ScoredProperty name="psk:PagesPerSheet"><psf:Value xsi:type="xsd:integer">1</psf:Value></psf:ScoredProperty>
		    </psf:Option>
		    <psf:Option constrained="psk:None">
		      <psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">4</psf:Value></psf:Property>
		      <psf:ScoredProperty name="psk:PagesPerSheet"><psf:Value xsi:type="xsd:integer">4</psf:Value></psf:ScoredProperty>
		    </psf:Option>
		  </psf:Feature>
		  <psf:Feature name="ns0000:JobImageType">
		    <psf:Option name="ns0000:JPEGMed" />
		    <psf:Option name="ns0000:PNG" constrained="psk:PrintTicketSettings"><psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">PNG - Lossless compression</psf:Value></psf:Property></psf:Option>
		  </psf:Feature>
		  <psf:Feature name="local:JobFinisher" xmlns:local="http://example.com/finisher">
		    <psf:Option name="local:Fold" />
		  </psf:Feature>
		  <psf:Feature name="psk:JobDeviceLanguage">
		    <psf:Option constrained="psk:DeviceSettings"><psf:ScoredProperty name="psk:Language"><psf:Value xsi:type="xsd:string">PCL</psf:Value></psf:ScoredProperty></psf:Option>
		  </psf:Feature>
		  <psf:ParameterDef name="ns0000:JobDarkness">
		    <psf:Property name="psf:DataType"><psf:Value xsi:type="xsd:QName">xsd:integer</psf:Value></psf:Property>
		    <psf:Property name="psf:MinValue"><psf:Value xsi:type="xsd:integer">-5</psf:Value></psf:Property>
		    <psf:Property name="psf:MaxValue"><psf:Value xsi:type="xsd:integer">5</psf:Value></psf:Property>
		    <psf:Property name="psf:DefaultValue"><psf:Value xsi:type="xsd:integer">0</psf:Value></psf:Property>
		    <psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">Darkness</psf:Value></psf:Property>
		  </psf:ParameterDef>
		  <psf:ParameterDef name="ns0000:DocumentUserPassword">
		    <psf:Property name="psf:DataType"><psf:Value xsi:type="xsd:QName">xsd:string</psf:Value></psf:Property>
		  </psf:ParameterDef>
		  <psf:ParameterDef name="ns0000:DocumentOwnerPassword">
		    <psf:Property name="psf:DataType"><psf:Value xsi:type="xsd:QName">xsd:string</psf:Value></psf:Property>
		    <psf:Property name="psf:UnitType"><psf:Value xsi:type="xsd:string">EncryptedOwnerPassword</psf:Value></psf:Property>
		  </psf:ParameterDef>
		  <psf:Property name="psk:PageImageableSize" />
		</psf:PrintCapabilities>
		""";

	/// <summary>
	/// The driver's defaults, with the vendor namespace under another prefix, as a ticket may declare it.
	/// </summary>
	private const string DefaultTicket = """
		<?xml version="1.0" encoding="UTF-8"?>
		<psf:PrintTicket xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" version="1" xmlns:vendor="http://schemas.microsoft.com/windows/printing/oemdriverpt/Office Inkjet/10.0.1/" xmlns:keywords="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
		  <psf:Feature name="keywords:PageOutputQuality"><psf:Option name="keywords:Normal" /></psf:Feature>
		  <psf:Feature name="keywords:DocumentNUp"><psf:Option><psf:ScoredProperty name="keywords:PagesPerSheet"><psf:Value xsi:type="xsd:integer">1</psf:Value></psf:ScoredProperty></psf:Option></psf:Feature>
		  <psf:Feature name="vendor:JobImageType"><psf:Option name="vendor:JPEGMed" /></psf:Feature>
		  <psf:ParameterInit name="vendor:JobDarkness"><psf:Value xsi:type="xsd:integer">2</psf:Value></psf:ParameterInit>
		</psf:PrintTicket>
		""";

	[Fact]
	public void GivenCapabilitiesWhenToVendorOptionsThenListsFeaturesThenParametersWithTheDriversNames()
	{
		// Arrange
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();

		// Act
		IReadOnlyList<VendorOption> options = document.ToVendorOptions(Encoding.UTF8.GetBytes(PrintCapabilitiesDocumentTests.DefaultTicket));

		// Assert — what the typed options cover, the DEVMODE snapshot, a feature with nothing to choose, and a secret
		// the driver expects encrypted are left out
		Assert.Equal
		(
			[
				"windows:feature:psk:PageOutputQuality",
				"windows:feature:psk:DocumentNUp",
				"windows:feature:ns0000:JobImageType",
				"windows:feature:local:JobFinisher",
				"windows:parameter:ns0000:JobDarkness",
				"windows:parameter:ns0000:DocumentUserPassword",
			],
			options.Select(option => option.Key)
		);
	}

	[Fact]
	public void GivenCapabilitiesWhenToVendorOptionsThenChoicesLeaveOutWhatTheDeviceOrAnAdministratorRulesOut()
	{
		// Arrange — psk:PrintTicketSettings only means another setting would have to change, so it stays
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();

		// Act
		IReadOnlyList<VendorOption> options = document.ToVendorOptions(Encoding.UTF8.GetBytes(PrintCapabilitiesDocumentTests.DefaultTicket));

		// Assert
		VendorOption quality = options[0];
		Assert.Equal("Output Quality", quality.DisplayName);
		Assert.Equal(["psk:High", "psk:Normal"], quality.Choices.Select(choice => choice.Value));
		Assert.Equal(["Best", "Normal"], quality.Choices.Select(choice => choice.DisplayName));
		Assert.Equal("psk:Normal", quality.DefaultValue);

		VendorOption images = options[2];
		Assert.Equal("Job image type", images.DisplayName);
		Assert.Equal(["ns0000:JPEGMed", "ns0000:PNG"], images.Choices.Select(choice => choice.Value));
		Assert.Equal(["JPEGMed", "PNG - Lossless compression"], images.Choices.Select(choice => choice.DisplayName));
		Assert.Equal("ns0000:JPEGMed", images.DefaultValue);
	}

	[Fact]
	public void GivenOptionsWithoutNamesWhenToVendorOptionsThenTheirScoredPropertiesIdentifyThem()
	{
		// Arrange
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();

		// Act
		VendorOption pages = document.ToVendorOptions(Encoding.UTF8.GetBytes(PrintCapabilitiesDocumentTests.DefaultTicket))[1];

		// Assert — the default ticket's option, under its own prefix, matches the same way
		Assert.Equal(["psk:PagesPerSheet=1", "psk:PagesPerSheet=4"], pages.Choices.Select(choice => choice.Value));
		Assert.Equal(["1", "4"], pages.Choices.Select(choice => choice.DisplayName));
		Assert.Equal("psk:PagesPerSheet=1", pages.DefaultValue);
	}

	[Fact]
	public void GivenParametersWhenToVendorOptionsThenNumbersHaveTheirLimitsAndTheTicketsValue()
	{
		// Arrange
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();

		// Act
		IReadOnlyList<VendorOption> options = document.ToVendorOptions(Encoding.UTF8.GetBytes(PrintCapabilitiesDocumentTests.DefaultTicket));

		// Assert — the ticket's value wins over the definition's default
		VendorOption darkness = Assert.Single(options, option => option.Key == "windows:parameter:ns0000:JobDarkness");
		Assert.Equal(VendorOptionKind.Number, darkness.Kind);
		Assert.Equal("Darkness", darkness.DisplayName);
		Assert.Equal(-5, darkness.MinimumValue);
		Assert.Equal(5, darkness.MaximumValue);
		Assert.Equal("2", darkness.DefaultValue);

		VendorOption password = Assert.Single(options, option => option.Key == "windows:parameter:ns0000:DocumentUserPassword");
		Assert.Equal(VendorOptionKind.Text, password.Kind);
		Assert.Equal("Document user password", password.DisplayName);
		Assert.Null(password.DefaultValue);
	}

	[Fact]
	public void GivenNoDefaultTicketWhenToVendorOptionsThenListsWithTheDefinitionsDefaultsOnly()
	{
		// Arrange
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();

		// Act
		IReadOnlyList<VendorOption> options = document.ToVendorOptions(null);

		// Assert
		Assert.Null(options[0].DefaultValue);
		Assert.Equal("0", Assert.Single(options, option => option.Key == "windows:parameter:ns0000:JobDarkness").DefaultValue);
	}

	[Fact]
	public void GivenFeatureAndParameterKeysWhenCreateTicketThenCopiesTheOptionsAndSetsTheValues()
	{
		// Arrange
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();

		Dictionary<string, string> vendorOptions = new Dictionary<string, string>
		{
			["windows:feature:psk:DocumentNUp"]      = "psk:PagesPerSheet=4",
			["windows:feature:ns0000:JobImageType"]  = "ns0000:PNG",
			["windows:parameter:ns0000:JobDarkness"] = "-3",
			["ipp:print-quality"]                    = "5"
		};

		// Act
		byte[]? ticket = document.CreateTicket(PrintCapabilitiesDocumentTests.Printer, vendorOptions, new PrintOptions());

		// Assert — the option itself, scored properties and all, without its display names or constraint
		Assert.NotNull(ticket);
		XElement root = XDocument.Parse(Encoding.UTF8.GetString(ticket)).Root!;
		XNamespace psf = "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework";
		Assert.Equal(psf + "PrintTicket", root.Name);

		XElement nUp = Assert.Single(root.Elements(psf + "Feature"), feature => feature.Attribute("name")?.Value == "psk:DocumentNUp");
		XElement option = Assert.Single(nUp.Elements(psf + "Option"));
		Assert.Null(option.Attribute("constrained"));
		Assert.Empty(option.Elements(psf + "Property"));
		Assert.Equal("4", option.Element(psf + "ScoredProperty")?.Element(psf + "Value")?.Value);

		XElement images = Assert.Single(root.Elements(psf + "Feature"), feature => feature.Attribute("name")?.Value == "ns0000:JobImageType");
		Assert.Equal("ns0000:PNG", images.Element(psf + "Option")?.Attribute("name")?.Value);
		Assert.Equal("http://schemas.microsoft.com/windows/printing/oemdriverpt/Office Inkjet/10.0.1/", root.GetNamespaceOfPrefix("ns0000")?.NamespaceName);

		XElement darkness = Assert.Single(root.Elements(psf + "ParameterInit"));
		Assert.Equal("ns0000:JobDarkness", darkness.Attribute("name")?.Value);
		Assert.Equal("-3", darkness.Element(psf + "Value")?.Value);
		Assert.Equal("xsd:integer", darkness.Element(psf + "Value")?.Attribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"))?.Value);
	}

	[Fact]
	public void GivenNoFeatureKeysWhenCreateTicketThenReturnsNull()
	{
		// Arrange
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();
		Dictionary<string, string> vendorOptions = new Dictionary<string, string> { ["windows:devmode"] = "AAAA" };

		// Act
		byte[]? ticket = document.CreateTicket(PrintCapabilitiesDocumentTests.Printer, vendorOptions, new PrintOptions());

		// Assert
		Assert.Null(ticket);
	}

	[Theory]
	[InlineData("windows:feature:psk:JobStapleAllDocuments", "psk:StapleTopLeft", "no feature or parameter 'psk:JobStapleAllDocuments'")]
	[InlineData("windows:feature:psk:PageOutputQuality", "psk:Photographic", "It offers psk:High, psk:Normal.")]
	[InlineData("windows:parameter:ns0000:JobDarkness", "6", "from -5 to 5")]
	[InlineData("windows:parameter:ns0000:JobDarkness", "dark", "not 'dark'")]
	[InlineData("windows:parameter:ns0000:JobBrightness", "1", "no feature or parameter 'ns0000:JobBrightness'")]
	public void GivenKeyThePrinterDoesNotOfferWhenCreateTicketThenThrowsArgumentExceptionSayingWhy(string key, string value, string expected)
	{
		// Arrange — a missing feature, an unknown option, a number out of range or not a number (provided by InlineData above)
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();
		Dictionary<string, string> vendorOptions = new Dictionary<string, string> { [key] = value };

		// Act
		Action create = () => document.CreateTicket(PrintCapabilitiesDocumentTests.Printer, vendorOptions, new PrintOptions());

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(create);
		Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
		Assert.Contains(key, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenOptionAnAdministratorRulesOutWhenCreateTicketThenThrowsArgumentExceptionSayingSo()
	{
		// Arrange — not listed, so not accepted either: the driver would swap it for another without saying
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();
		Dictionary<string, string> vendorOptions = new Dictionary<string, string> { ["windows:feature:psk:PageOutputQuality"] = "psk:Draft" };

		// Act
		Action create = () => document.CreateTicket(PrintCapabilitiesDocumentTests.Printer, vendorOptions, new PrintOptions());

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(create);
		Assert.Contains("rules out the option 'psk:Draft'", exception.Message, StringComparison.Ordinal);
		Assert.Contains("It offers psk:High, psk:Normal.", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenFeatureWhosePrefixIsDeclaredOnItselfWhenCreateTicketThenTheTicketDeclaresItToo()
	{
		// Arrange — the root doesn't declare local:, so the ticket couldn't resolve the copied names without it
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();
		Dictionary<string, string> vendorOptions = new Dictionary<string, string> { ["windows:feature:local:JobFinisher"] = "local:Fold" };

		// Act
		byte[]? ticket = document.CreateTicket(PrintCapabilitiesDocumentTests.Printer, vendorOptions, new PrintOptions());

		// Assert
		Assert.NotNull(ticket);
		XNamespace psf = "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework";
		XElement feature = Assert.Single(XDocument.Parse(Encoding.UTF8.GetString(ticket)).Root!.Elements(psf + "Feature"));
		Assert.Equal("http://example.com/finisher", feature.GetNamespaceOfPrefix("local")?.NamespaceName);
		Assert.Equal("local:Fold", feature.Element(psf + "Option")?.Attribute("name")?.Value);
	}

	[Fact]
	public void GivenDefaultTicketThatIsNotXmlWhenToVendorOptionsThenThrowsXmlExceptionForTheCallerToLog()
	{
		// Arrange
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();

		// Act
		Action list = () => document.ToVendorOptions(Encoding.UTF8.GetBytes("<not"));

		// Assert
		Assert.Throws<XmlException>(list);
	}

	[Theory]
	[InlineData("windows:feature:psk:PageOrientation", "psk:Portrait", "Orientation")]
	[InlineData("windows:parameter:psk:JobCopiesAllDocuments", "2", "Copies")]
	public void GivenFeatureAJobAlwaysSetsWhenCreateTicketThenThrowsArgumentExceptionNamingTheTypedOption(string key, string value, string property)
	{
		// Arrange — copies have a default, and the orientation follows the document (provided by InlineData above)
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();
		Dictionary<string, string> vendorOptions = new Dictionary<string, string> { [key] = value };

		// Act
		Action create = () => document.CreateTicket(PrintCapabilitiesDocumentTests.Printer, vendorOptions, new PrintOptions());

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(create);
		Assert.Contains($"PrintOptions.{property}", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenPaperFeatureWhenCreateTicketThenAcceptsItUnlessMediaSizeIsSet()
	{
		// Arrange — like IPP, a typed option and a vendor option may not both set the same thing
		PrintCapabilitiesDocument document = PrintCapabilitiesDocumentTests.Parse();
		Dictionary<string, string> vendorOptions = new Dictionary<string, string> { ["windows:feature:psk:PageMediaSize"] = "psk:ISOA4" };
		MediaSize a4 = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimetres = 210, HeightMillimetres = 297 };

		// Act
		byte[]? alone = document.CreateTicket(PrintCapabilitiesDocumentTests.Printer, vendorOptions, new PrintOptions());
		Action both = () => document.CreateTicket(PrintCapabilitiesDocumentTests.Printer, vendorOptions, new PrintOptions { MediaSize = a4 });

		// Assert
		Assert.NotNull(alone);
		ArgumentException exception = Assert.Throws<ArgumentException>(both);
		Assert.Contains("PrintOptions.MediaSize", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("<psf:PrintTicket xmlns:psf=\"http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework\" />")]
	[InlineData("not XML")]
	[InlineData("<!DOCTYPE x [<!ENTITY e \"e\">]><x>&e;</x>")]
	public void GivenDocumentThatIsNotPrintCapabilitiesWhenParseThenThrowsXmlException(string text)
	{
		// Arrange — a ticket, text, and a document type definition (provided by InlineData above)
		byte[] bytes = Encoding.UTF8.GetBytes(text);

		// Act
		Action parse = () => PrintCapabilitiesDocument.Parse(bytes);

		// Assert
		Assert.Throws<XmlException>(parse);
	}

	/// <summary>
	/// Parses <see cref="Capabilities"/>.
	/// </summary>
	/// <returns>The document.</returns>
	private static PrintCapabilitiesDocument Parse()
	{
		return PrintCapabilitiesDocument.Parse(Encoding.UTF8.GetBytes(PrintCapabilitiesDocumentTests.Capabilities));
	}
}
