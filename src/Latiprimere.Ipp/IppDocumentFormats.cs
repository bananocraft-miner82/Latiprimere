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
		Pdf = "application/pdf",
		Raw = "application/vnd.cups-raw"
	};

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
	/// Gets the MIME type for a Latiprimere document format.
	/// </summary>
	/// <param name="format">The document format.</param>
	/// <returns>The MIME type to send as <c>document-format</c>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a known format.</exception>
	public string GetMimeType(DocumentFormat format)
	{
		return format switch
		{
			DocumentFormat.Pdf => this.Pdf,
			DocumentFormat.Raw => this.Raw,
			_ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
		};
	}
}
