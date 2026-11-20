using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Latiprimere.Windows.Resources;

namespace Latiprimere.Windows.PrintTickets;

/// <summary>
/// A printer driver's Print Capabilities document: its features, their options, and its parameters (ADR 0032).
/// </summary>
/// <remarks>
/// <para>
/// It lists them as <see cref="VendorOption"/>s, and turns <c>windows:feature:</c> and <c>windows:parameter:</c>
/// vendor options back into a Print Ticket, checked against the same document.
/// </para>
/// <para>
/// Names are written with the prefixes the document declares, such as <c>ns0000:JobImageType</c>, and
/// <c>psk:</c> for the Print Schema's own keywords. Prefixes are used, not namespace URIs, because a driver's
/// namespace can include its version (Microsoft Print to PDF's does), so it changes when the driver is updated, while
/// the prefix the Print Ticket API gives it stays the same. An option without a name, such as one of Pages per sheet's,
/// is identified by its scored properties instead, as in <c>psk:PagesPerSheet=4</c>.
/// </para>
/// <para>
/// Only top-level features and parameters are read. Settings the typed options in <see cref="PrintOptions"/> cover
/// aren't listed (see <see cref="FindTypedOption"/>), nor are the driver's own plumbing, such as a DEVMODE snapshot,
/// or secrets, such as a PDF password.
/// </para>
/// </remarks>
internal sealed class PrintCapabilitiesDocument
{
	/// <summary>The key prefix for a feature, followed by its name.</summary>
	internal const string FeaturePrefix = "windows:feature:";

	/// <summary>The key prefix for a parameter, followed by its name.</summary>
	internal const string ParameterPrefix = "windows:parameter:";

	private const string KeywordsPrefix = "psk";                      // The prefix used for the Print Schema keywords
	private const string FrameworkPrefix = "psf";                     // The usual prefix of the Print Schema framework
	private const string SchemaInstancePrefix = "xsi";                // The usual prefix of XML Schema instances
	private const string SchemaPrefix = "xsd";                        // The usual prefix of XML Schema types
	private const string NameAttribute = "name";                      // A feature's, option's or property's name
	private const string ConstrainedAttribute = "constrained";        // What rules an option out, if anything
	private const string VersionAttribute = "version";                // The Print Schema version of a ticket
	private const string SchemaVersion = "1";                         // The only Print Schema version
	private const string TypeAttribute = "type";                      // xsi:type, a value's XML Schema type
	private const string DisplayNameProperty = "DisplayName";         // psk:DisplayName, a name to show people
	private const string DataTypeProperty = "DataType";               // psf:DataType, a parameter's value type
	private const string UnitTypeProperty = "UnitType";               // psf:UnitType, what a parameter's value means
	private const string MinValueProperty = "MinValue";               // psf:MinValue, a number's lowest value
	private const string MaxValueProperty = "MaxValue";               // psf:MaxValue, a number's highest value
	private const string DefaultValueProperty = "DefaultValue";       // psf:DefaultValue, the definition's default
	private const string Base64Unit = "base64";                       // A blob of the driver's, such as a DEVMODE
	private const string EncryptedUnitPrefix = "Encrypted";           // A secret, such as EncryptedUserPassword
	private const string StringType = "xsd:string";                   // A parameter's type when it doesn't say
	private const string MediaSizeFamily = "PageMediaSize";           // PageMediaSize and its custom-size parameters

	private static readonly XNamespace Framework = "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework";
	private static readonly XNamespace Keywords = "http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords";
	private static readonly XNamespace SchemaInstance = "http://www.w3.org/2001/XMLSchema-instance";
	private static readonly XNamespace Schema = "http://www.w3.org/2001/XMLSchema";

	private static readonly XName PrintCapabilitiesElement = PrintCapabilitiesDocument.Framework + "PrintCapabilities";
	private static readonly XName PrintTicketElement = PrintCapabilitiesDocument.Framework + "PrintTicket";
	private static readonly XName FeatureElement = PrintCapabilitiesDocument.Framework + "Feature";
	private static readonly XName OptionElement = PrintCapabilitiesDocument.Framework + "Option";
	private static readonly XName ParameterDefElement = PrintCapabilitiesDocument.Framework + "ParameterDef";
	private static readonly XName ParameterInitElement = PrintCapabilitiesDocument.Framework + "ParameterInit";
	private static readonly XName PropertyElement = PrintCapabilitiesDocument.Framework + "Property";
	private static readonly XName ScoredPropertyElement = PrintCapabilitiesDocument.Framework + "ScoredProperty";
	private static readonly XName ValueElement = PrintCapabilitiesDocument.Framework + "Value";

	/// <summary>Options a driver can't offer on this device or for this user, which a job can't choose.</summary>
	private static readonly string[] UnavailableConstraints = ["AdminSettings", "DeviceSettings"];

	/// <summary>XML Schema types of whole numbers, which a parameter's psf:DataType may name.</summary>
	private static readonly HashSet<string> IntegerTypes = new HashSet<string>(StringComparer.Ordinal)
	{
		"integer", "int", "long", "short", "nonNegativeInteger", "positiveInteger",
	};

	/// <summary>
	/// The Print Schema keywords the typed options cover, by local name, with the <see cref="PrintOptions"/> property
	/// that covers each. Names starting <see cref="MediaSizeFamily"/> belong to <see cref="PrintOptions.MediaSize"/>.
	/// </summary>
	private static readonly Dictionary<string, string> TypedKeywords = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["PageOrientation"] = nameof(PrintOptions.Orientation),
		["JobCopiesAllDocuments"] = nameof(PrintOptions.Copies),
		["DocumentCopiesAllPages"] = nameof(PrintOptions.Copies),
		["PageCopies"] = nameof(PrintOptions.Copies),
		["PageOutputColor"] = nameof(PrintOptions.ColorMode),
		["JobDuplexAllDocumentsContiguously"] = nameof(PrintOptions.Duplex),
		["DocumentDuplex"] = nameof(PrintOptions.Duplex),
		["JobInputBin"] = nameof(PrintOptions.InputTray),
		["DocumentInputBin"] = nameof(PrintOptions.InputTray),
		["PageInputBin"] = nameof(PrintOptions.InputTray),
	};

	private readonly XElement root;                                 // The PrintCapabilities element
	private readonly Dictionary<string, XElement> features;         // Top-level features, by name
	private readonly Dictionary<string, XElement> parameters;       // Parameter definitions, by name

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintCapabilitiesDocument"/> class.
	/// </summary>
	/// <param name="root">The PrintCapabilities element.</param>
	private PrintCapabilitiesDocument(XElement root)
	{
		this.root = root;
		this.features = new Dictionary<string, XElement>(StringComparer.Ordinal);
		this.parameters = new Dictionary<string, XElement>(StringComparer.Ordinal);

		foreach (XElement element in root.Elements())
		{
			string? name = element.Attribute(PrintCapabilitiesDocument.NameAttribute)?.Value;

			if (name is null)
			{
				continue;
			}

			if (element.Name == PrintCapabilitiesDocument.FeatureElement)
			{
				this.features.TryAdd(this.Normalise(element, name), element);
			}
			else if (element.Name == PrintCapabilitiesDocument.ParameterDefElement)
			{
				this.parameters.TryAdd(this.Normalise(element, name), element);
			}
		}
	}

	/// <summary>
	/// Reads a Print Capabilities document.
	/// </summary>
	/// <param name="document">The document, as the Print Ticket API wrote it.</param>
	/// <returns>The document.</returns>
	/// <exception cref="XmlException">It isn't XML, or isn't a Print Capabilities document.</exception>
	internal static PrintCapabilitiesDocument Parse(byte[] document)
	{
		XElement root = PrintCapabilitiesDocument.Load(document);

		if (root.Name != PrintCapabilitiesDocument.PrintCapabilitiesElement)
		{
			throw new XmlException(ExceptionMessages.FormatPrintSchemaRootUnexpected(root.Name, PrintCapabilitiesDocument.PrintCapabilitiesElement));
		}

		return new PrintCapabilitiesDocument(root);
	}

	/// <summary>
	/// Finds the typed option that covers a feature or parameter, if one does.
	/// </summary>
	/// <param name="name">The feature or parameter's name, such as <c>psk:PageMediaSize</c>.</param>
	/// <param name="options">The job's options, or <see langword="null"/> when listing.</param>
	/// <returns>
	/// The <see cref="PrintOptions"/> property's name, and whether the job sets it; <see langword="null"/> for a
	/// feature no typed option covers. Copies and orientation are always set: copies have a default, and the
	/// orientation follows the document's first page.
	/// </returns>
	internal static (string Property, bool IsSet)? FindTypedOption(string name, PrintOptions? options)
	{
		string prefix = PrintCapabilitiesDocument.KeywordsPrefix + ":";

		if (!name.StartsWith(prefix, StringComparison.Ordinal))
		{
			return null;
		}

		string local = name[prefix.Length..];

		string? property = local.StartsWith(PrintCapabilitiesDocument.MediaSizeFamily, StringComparison.Ordinal)
			? nameof(PrintOptions.MediaSize)
			: PrintCapabilitiesDocument.TypedKeywords.GetValueOrDefault(local);

		bool isSet = property switch
		{
			nameof(PrintOptions.Orientation) or nameof(PrintOptions.Copies) => true,
			nameof(PrintOptions.ColorMode) => options?.ColorMode is not null and not ColorMode.PrinterDefault,
			nameof(PrintOptions.Duplex) => options?.Duplex is not null and not DuplexMode.PrinterDefault,
			nameof(PrintOptions.InputTray) => options?.InputTray is not null,
			nameof(PrintOptions.MediaSize) => options?.MediaSize is not null,
			_ => false,
		};

		return property is null ? null : (property, isSet);
	}

	/// <summary>
	/// Lists the features and parameters as vendor options, in the document's order, features first.
	/// </summary>
	/// <param name="defaultTicket">The driver's default Print Ticket, for each option's default; or
	/// <see langword="null"/>.</param>
	/// <returns>The options; empty if there are none to offer.</returns>
	/// <exception cref="XmlException">The default ticket isn't XML: list without it, and say so.</exception>
	internal IReadOnlyList<VendorOption> ToVendorOptions(byte[]? defaultTicket)
	{
		(Dictionary<string, string> defaultChoices, Dictionary<string, string> defaultValues) = this.ReadDefaults(defaultTicket);
		List<VendorOption> options = [];

		foreach ((string name, XElement feature) in this.features)
		{
			if (PrintCapabilitiesDocument.FindTypedOption(name, null) is not null)
			{
				continue;
			}

			List<VendorOptionChoice> choices = this.ReadOptions(feature)
				.Where(option => !option.IsUnavailable)
				.Select(option => option.Choice)
				.DistinctBy(choice => choice.Value, StringComparer.Ordinal)
				.ToList();

			if (choices.Count == 0)
			{
				continue;
			}

			options.Add(new VendorOption
			{
				Key          = PrintCapabilitiesDocument.FeaturePrefix + name,
				DisplayName  = PrintCapabilitiesDocument.ReadDisplayName(feature) ?? PrintCapabilitiesDocument.DescribeName(name),
				Kind         = VendorOptionKind.Choice,
				Choices      = choices,
				DefaultValue = defaultChoices.GetValueOrDefault(name)
			});
		}

		foreach ((string name, XElement parameter) in this.parameters)
		{
			if (PrintCapabilitiesDocument.FindTypedOption(name, null) is not null || !PrintCapabilitiesDocument.IsListable(parameter))
			{
				continue;
			}

			bool isNumber = PrintCapabilitiesDocument.IsInteger(parameter);

			options.Add(new VendorOption
			{
				Key          = PrintCapabilitiesDocument.ParameterPrefix + name,
				DisplayName  = PrintCapabilitiesDocument.ReadDisplayName(parameter) ?? PrintCapabilitiesDocument.DescribeName(name),
				Kind         = isNumber ? VendorOptionKind.Number : VendorOptionKind.Text,
				DefaultValue = defaultValues.GetValueOrDefault(name) ?? PrintCapabilitiesDocument.ReadProperty(parameter, PrintCapabilitiesDocument.DefaultValueProperty),
				MinValue = isNumber ? PrintCapabilitiesDocument.ParseLong(PrintCapabilitiesDocument.ReadProperty(parameter, PrintCapabilitiesDocument.MinValueProperty)) : null,
				MaxValue = isNumber ? PrintCapabilitiesDocument.ParseLong(PrintCapabilitiesDocument.ReadProperty(parameter, PrintCapabilitiesDocument.MaxValueProperty)) : null
			});
		}

		return options;
	}

	/// <summary>
	/// Makes a delta Print Ticket from a job's <c>windows:feature:</c> and <c>windows:parameter:</c> vendor options,
	/// checking each against this document.
	/// </summary>
	/// <param name="printerName">The printer, for messages.</param>
	/// <param name="vendorOptions">The job's vendor options; other keys are ignored.</param>
	/// <param name="options">The job's options, whose typed settings mustn't be set twice.</param>
	/// <returns>The ticket, as UTF-8 XML; or <see langword="null"/> if there are no such keys.</returns>
	/// <exception cref="ArgumentException">A key names a feature or parameter the printer doesn't have, or one a
	/// typed option sets; or a value isn't one the printer offers, or one the device or an administrator rules
	/// out.</exception>
	internal byte[]? CreateTicket(string printerName, IReadOnlyDictionary<string, string> vendorOptions, PrintOptions options)
	{
		ArgumentNullException.ThrowIfNull(vendorOptions);
		ArgumentNullException.ThrowIfNull(options);

		List<XElement> settings = [];

		// Sorted, so the ticket is the same whatever order the caller's dictionary has
		foreach (KeyValuePair<string, string> option in vendorOptions.OrderBy(option => option.Key, StringComparer.Ordinal))
		{
			if (option.Key.StartsWith(PrintCapabilitiesDocument.FeaturePrefix, StringComparison.Ordinal))
			{
				string name = option.Key[PrintCapabilitiesDocument.FeaturePrefix.Length..];
				settings.Add(this.CreateFeature(printerName, option.Key, name, option.Value, options));
			}
			else if (option.Key.StartsWith(PrintCapabilitiesDocument.ParameterPrefix, StringComparison.Ordinal))
			{
				string name = option.Key[PrintCapabilitiesDocument.ParameterPrefix.Length..];
				settings.Add(this.CreateParameter(printerName, option.Key, name, option.Value, options));
			}
		}

		if (settings.Count == 0)
		{
			return null;
		}

		// The document's own namespace declarations, so names copied from it resolve; each copied element also
		// carries any declared further down, where it came from (see CopyDeclarations)
		XElement ticket = new XElement
		(
			PrintCapabilitiesDocument.PrintTicketElement,
			this.root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration && attribute.Name.Namespace != XNamespace.None),
			new XAttribute(PrintCapabilitiesDocument.VersionAttribute, PrintCapabilitiesDocument.SchemaVersion),
			settings
		);

		PrintCapabilitiesDocument.Declare(ticket, PrintCapabilitiesDocument.FrameworkPrefix, PrintCapabilitiesDocument.Framework);
		PrintCapabilitiesDocument.Declare(ticket, PrintCapabilitiesDocument.KeywordsPrefix, PrintCapabilitiesDocument.Keywords);
		PrintCapabilitiesDocument.Declare(ticket, PrintCapabilitiesDocument.SchemaInstancePrefix, PrintCapabilitiesDocument.SchemaInstance);
		PrintCapabilitiesDocument.Declare(ticket, PrintCapabilitiesDocument.SchemaPrefix, PrintCapabilitiesDocument.Schema);

		return PrintCapabilitiesDocument.Save(ticket);
	}

	/// <summary>
	/// Reads a Print Schema document, refusing document type definitions.
	/// </summary>
	/// <param name="document">The bytes.</param>
	/// <returns>The root element.</returns>
	/// <exception cref="XmlException">It isn't XML, or has no root element.</exception>
	private static XElement Load(byte[] document)
	{
		using MemoryStream stream = new MemoryStream(document, writable: false);
		using XmlReader reader = XmlReader.Create(stream, PrintSchemaText.CreateReaderSettings());

		return XDocument.Load(reader).Root ?? throw new XmlException(ExceptionMessages.PrintSchemaDocumentEmpty);
	}

	/// <summary>
	/// Writes a ticket as UTF-8 XML, with a declaration.
	/// </summary>
	/// <param name="ticket">The ticket.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Save(XElement ticket)
	{
		XmlWriterSettings settings = new XmlWriterSettings { Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false) };

		using MemoryStream output = new MemoryStream();

		using (XmlWriter writer = XmlWriter.Create(output, settings))
		{
			new XDocument(ticket).Save(writer);
		}

		return output.ToArray();
	}

	/// <summary>
	/// Declares a namespace on a ticket unless its prefix is already taken.
	/// </summary>
	/// <param name="ticket">The ticket.</param>
	/// <param name="prefix">The prefix.</param>
	/// <param name="ns">The namespace.</param>
	private static void Declare(XElement ticket, string prefix, XNamespace ns)
	{
		if (ticket.Attribute(XNamespace.Xmlns + prefix) is null)
		{
			ticket.Add(new XAttribute(XNamespace.Xmlns + prefix, ns.NamespaceName));
		}
	}

	/// <summary>
	/// Makes a name readable when the driver gives none: <c>ns0000:JobImageType</c> becomes "Job image type".
	/// </summary>
	/// <param name="name">The qualified name.</param>
	/// <returns>The description.</returns>
	private static string DescribeName(string name)
	{
		string local = name[(name.IndexOf(':', StringComparison.Ordinal) + 1)..];
		StringBuilder words = new StringBuilder();

		for (int index = 0; index < local.Length; index++)
		{
			char letter = local[index];

			if (letter == '_')
			{
				words.Append(' ');

				continue;
			}

			// A new word at a capital after a small letter or digit: "JobImage" → "Job image"
			bool startsWord = index > 0 && char.IsUpper(letter) && (char.IsLower(local[index - 1]) || char.IsDigit(local[index - 1]));

			if (startsWord)
			{
				words.Append(' ');
			}

			words.Append(words.Length == 0 ? char.ToUpperInvariant(letter) : startsWord ? char.ToLowerInvariant(letter) : letter);
		}

		string text = words.ToString().Trim();

		return text.Length == 0 ? name : char.ToUpperInvariant(text[0]) + text[1..];
	}

	/// <summary>
	/// Parses a whole number, or returns <see langword="null"/>.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The number.</returns>
	private static long? ParseLong(string? text)
	{
		return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value) ? value : null;
	}

	/// <summary>
	/// Reads the display name the driver gives an element, from its <c>psk:DisplayName</c> property.
	/// </summary>
	/// <param name="element">The feature, option or parameter.</param>
	/// <returns>The name, or <see langword="null"/> if the driver gives none.</returns>
	private static string? ReadDisplayName(XElement element)
	{
		string? name = PrintCapabilitiesDocument.ReadProperty(element, PrintCapabilitiesDocument.DisplayNameProperty);

		return string.IsNullOrWhiteSpace(name) ? null : name;
	}

	/// <summary>
	/// Reads one of an element's properties, such as a parameter's <c>psf:MinValue</c>, by its local name.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="localName">The property's local name.</param>
	/// <returns>The value, or <see langword="null"/>.</returns>
	private static string? ReadProperty(XElement element, string localName)
	{
		return element
			.Elements(PrintCapabilitiesDocument.PropertyElement)
			.Where(property => PrintCapabilitiesDocument.HasLocalName(property, localName))
			.Select(property => property.Element(PrintCapabilitiesDocument.ValueElement)?.Value?.Trim())
			.FirstOrDefault();
	}

	/// <summary>
	/// Checks whether a property's name has a local name, in any namespace.
	/// </summary>
	/// <param name="property">The property.</param>
	/// <param name="localName">The local name.</param>
	/// <returns><see langword="true"/> if it does.</returns>
	private static bool HasLocalName(XElement property, string localName)
	{
		string? name = property.Attribute(PrintCapabilitiesDocument.NameAttribute)?.Value;

		return name is not null && name.EndsWith(":" + localName, StringComparison.Ordinal);
	}

	/// <summary>
	/// Checks whether a parameter takes whole numbers.
	/// </summary>
	/// <param name="parameter">The parameter definition.</param>
	/// <returns><see langword="true"/> for <c>xsd:integer</c> and the other XML Schema integer types.</returns>
	private static bool IsInteger(XElement parameter)
	{
		string? type = PrintCapabilitiesDocument.ReadProperty(parameter, PrintCapabilitiesDocument.DataTypeProperty);
		string local = type is null ? string.Empty : type[(type.IndexOf(':', StringComparison.Ordinal) + 1)..];

		return PrintCapabilitiesDocument.IntegerTypes.Contains(local);
	}

	/// <summary>
	/// Checks whether a parameter is one to offer: not the driver's own plumbing, such as a DEVMODE snapshot in
	/// base64, and not a secret, such as a PDF password, whose value the driver expects encrypted and which would sit
	/// in plain text in the job's settings and spool files.
	/// </summary>
	/// <param name="parameter">The parameter definition.</param>
	/// <returns><see langword="true"/> to list it.</returns>
	private static bool IsListable(XElement parameter)
	{
		string? unit = PrintCapabilitiesDocument.ReadProperty(parameter, PrintCapabilitiesDocument.UnitTypeProperty);

		return !string.Equals(unit, PrintCapabilitiesDocument.Base64Unit, StringComparison.OrdinalIgnoreCase)
			   && unit?.StartsWith(PrintCapabilitiesDocument.EncryptedUnitPrefix, StringComparison.OrdinalIgnoreCase) != true;
	}

	/// <summary>
	/// Refuses a feature or parameter that a typed option the job sets already covers, as IPP refuses an attribute
	/// set twice (ADR 0026).
	/// </summary>
	/// <param name="key">The vendor option's key.</param>
	/// <param name="name">The feature or parameter's name.</param>
	/// <param name="options">The job's options.</param>
	/// <exception cref="ArgumentException">The typed option is set.</exception>
	private static void CheckNotTyped(string key, string name, PrintOptions options)
	{
		if (PrintCapabilitiesDocument.FindTypedOption(name, options) is (string property, true))
		{
			throw new ArgumentException(ExceptionMessages.FormatVendorFeatureSetByTypedOption(key, property));
		}
	}

	/// <summary>
	/// Lists the prefixed namespace declarations in scope where an element of this document is that the root doesn't
	/// already make, so a copy of it can carry them: names in it may use a prefix declared below the root, which the
	/// ticket, declaring only the root's, wouldn't otherwise resolve.
	/// </summary>
	/// <param name="source">The element copied from this document.</param>
	/// <returns>One declaration per prefix, the nearest one; usually none.</returns>
	private List<XAttribute> CopyDeclarations(XElement source)
	{
		return source
			.AncestorsAndSelf()
			.SelectMany(element => element.Attributes())
			.Where(attribute => attribute.IsNamespaceDeclaration && attribute.Name.Namespace != XNamespace.None)
			.DistinctBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal)
			.Where(attribute => this.root.Attribute(attribute.Name)?.Value != attribute.Value)
			.Select(attribute => new XAttribute(attribute))
			.ToList();
	}

	/// <summary>
	/// Writes a name with the prefix this document uses for its namespace; <c>psk</c> for the Print Schema keywords.
	/// </summary>
	/// <param name="context">The element the name appears on, whose namespace declarations resolve it.</param>
	/// <param name="name">The name as written, such as <c>ns0000:JobImageType</c>.</param>
	/// <returns>The name with this document's prefix; the name unchanged if its prefix isn't declared.</returns>
	private string Normalise(XElement context, string name)
	{
		int colon = name.IndexOf(':', StringComparison.Ordinal);

		if (colon <= 0)
		{
			return name;
		}

		XNamespace? ns = context.GetNamespaceOfPrefix(name[..colon]);

		if (ns is null)
		{
			return name;
		}

		string? prefix = ns == PrintCapabilitiesDocument.Keywords
			? PrintCapabilitiesDocument.KeywordsPrefix
			: this.root.GetPrefixOfNamespace(ns);

		return prefix is null ? name : prefix + name[colon..];
	}

	/// <summary>
	/// Lists a feature's options, with the value that chooses each and whether the driver can offer it here.
	/// </summary>
	/// <param name="feature">The feature.</param>
	/// <returns>The options; those without a name or scored properties to identify them are left out. An option is
	/// unavailable when the device or an administrator rules it out; one only the current settings rule out stays
	/// available, since another setting could allow it.</returns>
	private IEnumerable<(VendorOptionChoice Choice, XElement Element, bool IsUnavailable)> ReadOptions(XElement feature)
	{
		foreach (XElement option in feature.Elements(PrintCapabilitiesDocument.OptionElement))
		{
			string? value = this.ReadOptionValue(option);

			if (value is null)
			{
				continue;
			}

			string? constrained = option.Attribute(PrintCapabilitiesDocument.ConstrainedAttribute)?.Value;

			bool isUnavailable = constrained is not null
								 && PrintCapabilitiesDocument.UnavailableConstraints.Any(constraint => constrained.EndsWith(":" + constraint, StringComparison.Ordinal));

			string displayName = PrintCapabilitiesDocument.ReadDisplayName(option)
								 ?? (option.Attribute(PrintCapabilitiesDocument.NameAttribute) is null ? value : PrintCapabilitiesDocument.DescribeName(value));

			yield return (new VendorOptionChoice { Value = value, DisplayName = displayName }, option, isUnavailable);
		}
	}

	/// <summary>
	/// Reads the value that chooses an option: its name, or for an option without one, its scored properties.
	/// </summary>
	/// <param name="option">The option, in this document or in a ticket.</param>
	/// <returns>The value, or <see langword="null"/> if nothing identifies the option.</returns>
	private string? ReadOptionValue(XElement option)
	{
		string? name = option.Attribute(PrintCapabilitiesDocument.NameAttribute)?.Value;

		if (name is not null)
		{
			return this.Normalise(option, name);
		}

		// Pages per sheet's options, for example, are told apart only by psk:PagesPerSheet
		List<string> properties = option
			.Elements(PrintCapabilitiesDocument.ScoredPropertyElement)
			.Select(property => (Name: property.Attribute(PrintCapabilitiesDocument.NameAttribute)?.Value, Value: property.Element(PrintCapabilitiesDocument.ValueElement)?.Value))
			.Where(property => property.Name is not null && property.Value is not null)
			.Select(property => this.Normalise(option, property.Name!) + "=" + property.Value!.Trim())
			.ToList();

		return properties.Count == 0 ? null : string.Join(";", properties);
	}

	/// <summary>
	/// Reads the default ticket's choices and parameter values, by this document's names.
	/// </summary>
	/// <param name="defaultTicket">The ticket, or <see langword="null"/>.</param>
	/// <returns>The chosen option values by feature, and the parameter values by parameter.</returns>
	/// <exception cref="XmlException">The ticket isn't XML.</exception>
	private (Dictionary<string, string> Choices, Dictionary<string, string> Values) ReadDefaults(byte[]? defaultTicket)
	{
		Dictionary<string, string> choices = new Dictionary<string, string>(StringComparer.Ordinal);
		Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);

		if (defaultTicket is null)
		{
			return (choices, values);
		}

		XElement ticket = PrintCapabilitiesDocument.Load(defaultTicket);

		foreach (XElement element in ticket.Elements())
		{
			string? name = element.Attribute(PrintCapabilitiesDocument.NameAttribute)?.Value;

			if (name is null)
			{
				continue;
			}

			// The ticket declares its own prefixes; names are compared by this document's
			string normalised = this.Normalise(element, name);

			if (element.Name == PrintCapabilitiesDocument.FeatureElement
				&& element.Element(PrintCapabilitiesDocument.OptionElement) is XElement option
				&& this.ReadOptionValue(option) is string value)
			{
				choices.TryAdd(normalised, value);
			}
			else if (element.Name == PrintCapabilitiesDocument.ParameterInitElement
					 && element.Element(PrintCapabilitiesDocument.ValueElement)?.Value is string text)
			{
				values.TryAdd(normalised, text.Trim());
			}
		}

		return (choices, values);
	}

	/// <summary>
	/// Makes a ticket's Feature element choosing one option, copied from this document.
	/// </summary>
	/// <param name="printerName">The printer, for messages.</param>
	/// <param name="key">The vendor option's key, for messages.</param>
	/// <param name="name">The feature's name.</param>
	/// <param name="value">The option's value.</param>
	/// <param name="options">The job's options.</param>
	/// <returns>The element.</returns>
	private XElement CreateFeature(string printerName, string key, string name, string value, PrintOptions options)
	{
		PrintCapabilitiesDocument.CheckNotTyped(key, name, options);

		if (!this.features.TryGetValue(name, out XElement? feature))
		{
			throw new ArgumentException(ExceptionMessages.FormatVendorFeatureUnknown(printerName, name, key));
		}

		List<(VendorOptionChoice Choice, XElement Element, bool IsUnavailable)> all = this.ReadOptions(feature).ToList();
		string offered = string.Join(", ", all.Where(option => !option.IsUnavailable).Select(option => option.Choice.Value).Distinct(StringComparer.Ordinal));
		(VendorOptionChoice Choice, XElement Element, bool IsUnavailable) chosen = all.FirstOrDefault(option => option.Choice.Value == value);

		if (chosen.Element is null)
		{
			throw new ArgumentException(ExceptionMessages.FormatVendorFeatureOptionUnknown(printerName, value, key, offered));
		}

		// Listed as not offered, so refused too: the driver would only swap it for another option without saying
		if (chosen.IsUnavailable)
		{
			throw new ArgumentException(ExceptionMessages.FormatVendorFeatureOptionUnavailable(printerName, value, key, offered));
		}

		// The option as the driver describes it, scored properties and all, without its display names
		XElement copy = new XElement(chosen.Element);
		copy.Elements(PrintCapabilitiesDocument.PropertyElement).Remove();
		copy.Attribute(PrintCapabilitiesDocument.ConstrainedAttribute)?.Remove();

		return new XElement
		(
			PrintCapabilitiesDocument.FeatureElement,
			this.CopyDeclarations(chosen.Element),
			new XAttribute(PrintCapabilitiesDocument.NameAttribute, feature.Attribute(PrintCapabilitiesDocument.NameAttribute)!.Value),
			copy
		);
	}

	/// <summary>
	/// Makes a ticket's ParameterInit element, after checking the value suits the parameter.
	/// </summary>
	/// <param name="printerName">The printer, for messages.</param>
	/// <param name="key">The vendor option's key, for messages.</param>
	/// <param name="name">The parameter's name.</param>
	/// <param name="value">The value.</param>
	/// <param name="options">The job's options.</param>
	/// <returns>The element.</returns>
	private XElement CreateParameter(string printerName, string key, string name, string value, PrintOptions options)
	{
		PrintCapabilitiesDocument.CheckNotTyped(key, name, options);

		if (!this.parameters.TryGetValue(name, out XElement? parameter))
		{
			throw new ArgumentException(ExceptionMessages.FormatVendorFeatureUnknown(printerName, name, key));
		}

		string type = PrintCapabilitiesDocument.ReadProperty(parameter, PrintCapabilitiesDocument.DataTypeProperty) ?? PrintCapabilitiesDocument.StringType;

		if (PrintCapabilitiesDocument.IsInteger(parameter))
		{
			long? number = PrintCapabilitiesDocument.ParseLong(value);
			long? minimum = PrintCapabilitiesDocument.ParseLong(PrintCapabilitiesDocument.ReadProperty(parameter, PrintCapabilitiesDocument.MinValueProperty));
			long? maximum = PrintCapabilitiesDocument.ParseLong(PrintCapabilitiesDocument.ReadProperty(parameter, PrintCapabilitiesDocument.MaxValueProperty));

			if (number is null || number < minimum || number > maximum)
			{
				string range = ExceptionMessages.FormatVendorParameterRange
				(
					minimum?.ToString(CultureInfo.InvariantCulture) ?? ExceptionMessages.VendorParameterNoLimit,
					maximum?.ToString(CultureInfo.InvariantCulture) ?? ExceptionMessages.VendorParameterNoLimit
				);

				throw new ArgumentException(ExceptionMessages.FormatVendorParameterOutOfRange(key, range, printerName, value));
			}

			value = number.Value.ToString(CultureInfo.InvariantCulture);
		}

		return new XElement
		(
			PrintCapabilitiesDocument.ParameterInitElement,
			this.CopyDeclarations(parameter),
			new XAttribute(PrintCapabilitiesDocument.NameAttribute, parameter.Attribute(PrintCapabilitiesDocument.NameAttribute)!.Value),
			new XElement
			(
				PrintCapabilitiesDocument.ValueElement,
				new XAttribute(PrintCapabilitiesDocument.SchemaInstance + PrintCapabilitiesDocument.TypeAttribute, type),
				value
			)
		);
	}
}
