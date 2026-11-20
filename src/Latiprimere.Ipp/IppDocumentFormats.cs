namespace Latiprimere.Ipp;

/// <summary>
/// The IPP <c>document-format</c> MIME types a print system expects for each Latiprimere document format.
/// </summary>
internal sealed record IppDocumentFormats
{
	/// <summary>
	/// Formats for CUPS, which accepts raw data as <c>application/vnd.cups-raw</c> and sends it to the printer
	/// unfiltered.
	/// </summary>
	internal static readonly IppDocumentFormats Cups = new IppDocumentFormats
	{
		Pdf               = "application/pdf",
		Raw               = "application/vnd.cups-raw",
		ConvertsDocuments = true
	};

	/// <summary>The MIME type for JPEG images, which IPP Everywhere and AirPrint printers and CUPS take.</summary>
	internal const string Jpeg = "image/jpeg";

	/// <summary>The MIME type for PNG images.</summary>
	internal const string Png = "image/png";

	/// <summary>The MIME type for plain text, which CUPS lays out itself.</summary>
	internal const string Text = "text/plain";

	/// <summary>
	/// Formats for IPP printers, which treat <c>application/octet-stream</c> as data in the printer's own language.
	/// </summary>
	internal static readonly IppDocumentFormats Standard = new IppDocumentFormats
	{
		Pdf = "application/pdf",
		Raw = "application/octet-stream"
	};

	/// <summary>
	/// Gets the MIME type for PDF documents.
	/// </summary>
	public required string Pdf { get; init; }

	/// <summary>
	/// Gets the MIME type for raw printer-language data.
	/// </summary>
	public required string Raw { get; init; }

	/// <summary>
	/// Gets a value indicating whether the print system converts images and lays out text itself, as CUPS does, so
	/// they are sent as they are (ADR 0047, ADR 0048). A printer reached directly doesn't, whether or not it lists the
	/// formats it takes.
	/// </summary>
	public bool ConvertsDocuments { get; init; }

	/// <summary>
	/// Gets the MIME type for a Latiprimere document format.
	/// </summary>
	/// <param name="format">The document format.</param>
	/// <returns>The MIME type to send as <c>document-format</c>.</returns>
	/// <exception cref="NotSupportedException"><paramref name="format"/> is one IPP printing doesn't know, as
	/// <see cref="IPrinter.SubmitAsync"/> documents for a format a printer can't accept (ADR 0044).</exception>
	public string GetMimeType(DocumentFormat format)
	{
		return format switch
		{
			DocumentFormat.Pdf => this.Pdf,
			DocumentFormat.Raw => this.Raw,
			DocumentFormat.Jpeg => IppDocumentFormats.Jpeg,
			DocumentFormat.Png => IppDocumentFormats.Png,
			DocumentFormat.Text => IppDocumentFormats.Text,
			_ => throw new NotSupportedException(Resources.ExceptionMessages.FormatDocumentFormatUnknown(format)),
		};
	}
}
