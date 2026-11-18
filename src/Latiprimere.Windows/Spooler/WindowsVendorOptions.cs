using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.PrintTickets;
using Latiprimere.Windows.Resources;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// Reads the <c>windows:</c> keys of <see cref="PrintOptions.VendorOptions"/> (ADR 0026, ADR 0031).
/// </summary>
internal static partial class WindowsVendorOptions
{
	/// <summary>The prefix the Windows print system reads.</summary>
	internal const string Prefix = "windows:";

	/// <summary>A whole DEVMODE saved from the printer's driver, in base64.</summary>
	internal const string DevModeKey = "windows:devmode";

	/// <summary>A Print Ticket, in XML, merged onto the job's settings through the driver.</summary>
	internal const string PrintTicketKey = "windows:print-ticket";

	/// <summary>The Print Schema Framework namespace, which a Print Ticket's root element is in.</summary>
	internal const string PrintSchemaFrameworkNamespace = "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework";

	/// <summary>The Print Ticket's root element.</summary>
	private const string PrintTicketElement = "PrintTicket";

	/// <summary>The keys Windows supports, as the message for any other names them.</summary>
	private static readonly string SupportedKeys = $"{WindowsVendorOptions.DevModeKey}, {WindowsVendorOptions.PrintTicketKey}, "
												   + $"{PrintCapabilitiesDocument.FeaturePrefix}<name> and {PrintCapabilitiesDocument.ParameterPrefix}<name>";

	/// <summary>Where <c>dmSize</c> is in a DEVMODEW: after the 32-character device name and two versions.</summary>
	private const int SizeOffset = 68;

	/// <summary>Where <c>dmDriverExtra</c> is.</summary>
	private const int DriverExtraOffset = 70;

	/// <summary>
	/// The smallest public part accepted: up to <c>dmFormName</c>, which covers every member Latiprimere sets, so
	/// its changes land inside what the driver reads as the DEVMODE.
	/// </summary>
	private static readonly int MinimumSize = (int)Marshal.OffsetOf<DevMode>(nameof(DevMode.FormName));

	/// <summary>
	/// Reads the saved DEVMODE and the Print Ticket, after checking every <c>windows:</c> key is one Windows
	/// supports.
	/// </summary>
	/// <param name="vendorOptions">The vendor options.</param>
	/// <returns>The settings; <see cref="WindowsVendorSettings.None"/> if there are no <c>windows:</c> keys.</returns>
	/// <exception cref="NotSupportedException">A <c>windows:</c> key isn't one Windows supports.</exception>
	/// <exception cref="ArgumentException">The DEVMODE isn't base64, or isn't a DEVMODE; or the Print Ticket isn't
	/// XML, or isn't a Print Ticket.</exception>
	internal static WindowsVendorSettings Read(IReadOnlyDictionary<string, string> vendorOptions)
	{
		ArgumentNullException.ThrowIfNull(vendorOptions);

		// Unlike IPP, Windows has nowhere to pass an unknown setting on to
		string? unsupported = vendorOptions.Keys
			.Where(key => key.StartsWith(WindowsVendorOptions.Prefix, StringComparison.Ordinal))
			.Where(key => key is not WindowsVendorOptions.DevModeKey and not WindowsVendorOptions.PrintTicketKey)
			.Where(key => !WindowsVendorOptions.IsFeatureKey(key))
			.Order(StringComparer.Ordinal)
			.FirstOrDefault();

		if (unsupported is not null)
		{
			string message = ExceptionMessages.FormatVendorOptionNotSupported(unsupported, WindowsVendorOptions.SupportedKeys);

			throw new NotSupportedException(message);
		}

		byte[]? devMode = vendorOptions.TryGetValue(WindowsVendorOptions.DevModeKey, out string? encodedDevMode)
			? WindowsVendorOptions.DecodeDevMode(encodedDevMode)
			: null;

		byte[]? printTicket = vendorOptions.TryGetValue(WindowsVendorOptions.PrintTicketKey, out string? ticket)
			? WindowsVendorOptions.EncodePrintTicket(ticket)
			: null;

		// Whether each feature and parameter exists is checked against the printer's Print Capabilities, when printed
		Dictionary<string, string> features = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (KeyValuePair<string, string> option in vendorOptions.Where(option => WindowsVendorOptions.IsFeatureKey(option.Key)))
		{
			string prefix = option.Key.StartsWith(PrintCapabilitiesDocument.FeaturePrefix, StringComparison.Ordinal)
				? PrintCapabilitiesDocument.FeaturePrefix
				: PrintCapabilitiesDocument.ParameterPrefix;

			string name = option.Key[prefix.Length..];

			if (!WindowsVendorOptions.QualifiedNamePattern().IsMatch(name) || option.Value.Length == 0)
			{
				throw new ArgumentException(ExceptionMessages.FormatVendorFeatureNameInvalid(option.Key));
			}

			features.Add(option.Key, option.Value);
		}

		return devMode is null && printTicket is null && features.Count == 0
			? WindowsVendorSettings.None
			: new WindowsVendorSettings { DevMode = devMode, PrintTicket = printTicket, Features = features };
	}

	/// <summary>
	/// Checks whether a key chooses a Print Capabilities feature or sets a parameter.
	/// </summary>
	/// <param name="key">The key.</param>
	/// <returns><see langword="true"/> for <c>windows:feature:</c> and <c>windows:parameter:</c> keys.</returns>
	private static bool IsFeatureKey(string key)
	{
		return key.StartsWith(PrintCapabilitiesDocument.FeaturePrefix, StringComparison.Ordinal)
			   || key.StartsWith(PrintCapabilitiesDocument.ParameterPrefix, StringComparison.Ordinal);
	}

	/// <summary>
	/// A qualified XML name, as Print Schema names are written: a prefix, a colon and a local name, such as
	/// <c>psk:PageOutputQuality</c> or <c>ns0000:JobImageType</c>.
	/// </summary>
	/// <returns>The pattern.</returns>
	[GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.-]*:[A-Za-z_][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant)]
	private static partial Regex QualifiedNamePattern();

	/// <summary>
	/// Checks a Print Ticket is XML with a <c>psf:PrintTicket</c> root, and writes it out as UTF-8, with a
	/// declaration that says so, whatever encoding the text declared.
	/// </summary>
	/// <param name="printTicket">The Print Ticket.</param>
	/// <returns>The Print Ticket as UTF-8 XML.</returns>
	/// <exception cref="ArgumentException">The value isn't XML, or its root isn't a Print Ticket.</exception>
	/// <remarks>
	/// Whether its features and options mean anything is for the printer's driver to say, when the job is printed.
	/// Document type definitions are refused, so a ticket can't make the parser fetch or expand anything.
	/// </remarks>
	internal static byte[] EncodePrintTicket(string printTicket)
	{
		XmlWriterSettings writerSettings = new XmlWriterSettings
		{
			Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
		};

		using MemoryStream output = new MemoryStream();

		try
		{
			using StringReader text = new StringReader(printTicket);
			using XmlReader reader = XmlReader.Create(text, PrintSchemaText.CreateReaderSettings());

			reader.MoveToContent();

			if (reader.LocalName != WindowsVendorOptions.PrintTicketElement
				|| reader.NamespaceURI != WindowsVendorOptions.PrintSchemaFrameworkNamespace)
			{
				string root = reader.NamespaceURI.Length == 0 ? reader.LocalName : $"{{{reader.NamespaceURI}}}{reader.LocalName}";
				string message = ExceptionMessages.FormatPrintTicketNotPrintTicket(root, WindowsVendorOptions.PrintSchemaFrameworkNamespace);

				throw new ArgumentException(message);
			}

			using (XmlWriter writer = XmlWriter.Create(output, writerSettings))
			{
				writer.WriteStartDocument();
				writer.WriteNode(reader, defattr: true);
				writer.WriteEndDocument();
			}

			// Anything after the root element, such as a second root, is still checked
			while (reader.Read())
			{
			}
		}
		catch (XmlException exception)
		{
			throw new ArgumentException(ExceptionMessages.FormatPrintTicketNotXml(exception.Message), exception);
		}

		return output.ToArray();
	}

	/// <summary>
	/// Decodes a saved DEVMODE and checks its sizes add up.
	/// </summary>
	/// <param name="encoded">The DEVMODE in base64.</param>
	/// <returns>The DEVMODE.</returns>
	/// <exception cref="ArgumentException">The value isn't base64, or isn't a DEVMODE.</exception>
	private static byte[] DecodeDevMode(string encoded)
	{
		byte[] devMode;

		try
		{
			devMode = Convert.FromBase64String(encoded);
		}
		catch (FormatException exception)
		{
			throw new ArgumentException(ExceptionMessages.DevModeNotBase64, exception);
		}

		// dmSize and dmDriverExtra describe the buffer; a memory block from GlobalAlloc may be a little longer
		int size = devMode.Length > WindowsVendorOptions.DriverExtraOffset + 1
			? BinaryPrimitives.ReadUInt16LittleEndian(devMode.AsSpan(WindowsVendorOptions.SizeOffset))
			: 0;

		int driverExtra = size > 0
			? BinaryPrimitives.ReadUInt16LittleEndian(devMode.AsSpan(WindowsVendorOptions.DriverExtraOffset))
			: 0;

		if (size < WindowsVendorOptions.MinimumSize || size + driverExtra > devMode.Length)
		{
			string message = ExceptionMessages.FormatDevModeInvalid(devMode.Length, size, driverExtra);

			throw new ArgumentException(message);
		}

		return devMode;
	}
}
