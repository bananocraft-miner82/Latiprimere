namespace Latiprimere.Rendering.Pdfium;

/// <summary>
/// Limits on the PDFs <see cref="PdfiumRasterizer"/> opens and renders, for applications that print PDFs from
/// anyone, such as a web application's users.
/// </summary>
public sealed class PdfiumOptions
{
	/// <summary>
	/// The default longest PDF opened: 2,147,483,647 bytes, about 2 GB, the most PDFium can read from memory here.
	/// </summary>
	public static readonly int DefaultMaxDocumentBytes = int.MaxValue;

	/// <summary>
	/// The default longest a band of a page may take to render: 2 minutes.
	/// </summary>
	public static readonly TimeSpan DefaultRenderTimeout = TimeSpan.FromMinutes(2);

	/// <summary>
	/// The longest render timeout accepted: far more than any page needs, and no risk of overflowing a deadline.
	/// </summary>
	private static readonly TimeSpan MaxRenderTimeout = TimeSpan.FromDays(365);

	private int maxDocumentBytes = PdfiumOptions.DefaultMaxDocumentBytes; // Backs MaxDocumentBytes
	private TimeSpan renderTimeout = PdfiumOptions.DefaultRenderTimeout;  // Backs RenderTimeout

	/// <summary>
	/// Gets or sets the longest PDF opened, in bytes. Defaults to <see cref="DefaultMaxDocumentBytes"/>.
	/// </summary>
	/// <remarks>
	/// The whole PDF is held in memory while it is open, so a lower limit bounds what one document can take. A longer
	/// one is refused with a <see cref="PrintException"/> before more than the limit is read, and before PDFium sees
	/// any of it.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
	public int MaxDocumentBytes
	{
		get
		{
			return this.maxDocumentBytes;
		}
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
			this.maxDocumentBytes = value;
		}
	}

	/// <summary>
	/// Gets or sets the longest a band of a page may take to render, or <see cref="Timeout.InfiniteTimeSpan"/> for no
	/// limit. Defaults to <see cref="DefaultRenderTimeout"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A PDF can be made to keep a renderer busy for as long as its author likes, and only one PDF renders at a time
	/// in a process, as PDFium isn't thread-safe; so without a limit, one such PDF would hold up every other. When the
	/// limit runs out, the band is abandoned with a <see cref="PrintException"/>, and the next rendering can start.
	/// </para>
	/// <para>
	/// PDFium checks the time between the steps of drawing a page, so a single step that takes long, such as decoding
	/// one huge image, can overrun it. Reading a page's content, when it is first rendered, isn't counted.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The value is not positive, other than
	/// <see cref="Timeout.InfiniteTimeSpan"/>, or is longer than a year.</exception>
	public TimeSpan RenderTimeout
	{
		get
		{
			return this.renderTimeout;
		}
		set
		{
			if (value != Timeout.InfiniteTimeSpan)
			{
				ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
				ArgumentOutOfRangeException.ThrowIfGreaterThan(value, PdfiumOptions.MaxRenderTimeout);
			}

			this.renderTimeout = value;
		}
	}
}
