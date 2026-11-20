using SharpIpp.Protocol.Models;
using IppDocumentFormat = SharpIpp.Protocol.Models.DocumentFormat;
using IppPrinterState = SharpIpp.Protocol.Models.PrinterState;

namespace Latiprimere.Ipp.Testing.Builders;

/// <summary>
/// Builds IPP printer description attributes for tests. Defaults match the Virtual-IPP queue in the CUPS test
/// container (see tests/Latiprimere.Ipp.Tests/Data).
/// </summary>
public sealed class PrinterAttributesBuilder
{
	private string? name = "Virtual-IPP";                          // printer-name
	private string? info = "Virtual IPP Everywhere printer";       // printer-info
	private string? location = "Test container";                   // printer-location
	private string? makeAndModel = "Printer - IPP Everywhere";     // printer-make-and-model
	private IppPrinterState? state = IppPrinterState.Idle;          // printer-state
	private string[]? documentFormats = null;                      // document-format-supported
	private string? uuid = null;                                   // printer-uuid
	private Uri[]? uris = null;                                    // printer-uri-supported
	private string[]? authentications = null;                      // uri-authentication-supported

	/// <summary>Sets <c>printer-uuid</c>; omitted unless set.</summary>
	/// <param name="uuid">The UUID, usually as a <c>urn:uuid:</c> URN.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrinterAttributesBuilder WithUuid(string uuid)
	{
		this.uuid = uuid;

		return this;
	}

	/// <summary>
	/// Sets <c>printer-uri-supported</c> and the <c>uri-authentication-supported</c> beside it; omitted unless set.
	/// </summary>
	/// <param name="entries">Each address with the authentication it asks for, such as
	/// <c>("ipp://printer.local/ipp/print", "none")</c>.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrinterAttributesBuilder WithUriAuthentication(params (string Uri, string Authentication)[] entries)
	{
		this.uris = entries.Select(entry => new Uri(entry.Uri)).ToArray();
		this.authentications = entries.Select(entry => entry.Authentication).ToArray();

		return this;
	}

	/// <summary>Sets <c>document-format-supported</c>; omitted unless set.</summary>
	/// <param name="documentFormats">The MIME types the printer accepts.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrinterAttributesBuilder WithDocumentFormats(params string[] documentFormats)
	{
		this.documentFormats = documentFormats;

		return this;
	}

	/// <summary>Sets <c>printer-name</c>, or omits it when <see langword="null"/>.</summary>
	/// <param name="name">The queue name.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrinterAttributesBuilder WithName(string? name)
	{
		this.name = name;

		return this;
	}

	/// <summary>Sets <c>printer-state</c>, or omits it when <see langword="null"/>.</summary>
	/// <param name="state">The printer state.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrinterAttributesBuilder WithState(IppPrinterState? state)
	{
		this.state = state;

		return this;
	}

	/// <summary>Omits the descriptive attributes: info, location, and make and model.</summary>
	/// <returns>This builder, for chaining.</returns>
	public PrinterAttributesBuilder WithoutDescription()
	{
		this.info = null;
		this.location = null;
		this.makeAndModel = null;

		return this;
	}

	/// <summary>Builds the attributes.</summary>
	/// <returns>The printer description attributes.</returns>
	public PrinterDescriptionAttributes Build()
	{
		return new PrinterDescriptionAttributes
		{
			PrinterName         = PrinterAttributesBuilder.ToText(this.name),
			PrinterInfo         = PrinterAttributesBuilder.ToText(this.info),
			PrinterLocation     = PrinterAttributesBuilder.ToText(this.location),
			PrinterMakeAndModel = PrinterAttributesBuilder.ToText(this.makeAndModel),
			PrinterState        = this.state is null
				? (IppValue<IppPrinterState>?)null
				: new IppValue<IppPrinterState>(this.state.Value),
			DocumentFormatSupported = this.documentFormats is null
				? (IppValue<IppDocumentFormat[]>?)null
				: new IppValue<IppDocumentFormat[]>
				(
					this.documentFormats.Select(format => new IppDocumentFormat(format)).ToArray()
				),
			PrinterUUID = this.uuid is null ? (IppValue<string>?)null : new IppValue<string>(this.uuid),
			PrinterUriSupported = this.uris is null ? (IppValue<Uri[]>?)null : new IppValue<Uri[]>(this.uris),
			UriAuthenticationSupported = this.authentications is null
				? (IppValue<UriAuthentication[]>?)null
				: new IppValue<UriAuthentication[]>
				(
					this.authentications.Select(keyword => new UriAuthentication(keyword)).ToArray()
				)
		};
	}

	/// <summary>Wraps a string as an IPP text value.</summary>
	/// <param name="value">The string, or <see langword="null"/> for an absent attribute.</param>
	/// <returns>The IPP value.</returns>
	private static StringWithLanguage? ToText(string? value)
	{
		// The explicit cast keeps the attribute absent; a bare null would convert to an empty StringWithLanguage
		return value is null ? (StringWithLanguage?)null : new StringWithLanguage(value);
	}
}
