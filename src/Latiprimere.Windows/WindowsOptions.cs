namespace Latiprimere.Windows;

/// <summary>
/// Configures printing through the Windows print spooler.
/// </summary>
public sealed class WindowsOptions
{
	private int? maxRenderDpi; // Backs MaxRenderDpi; null renders at the printer's resolution

	/// <summary>
	/// Gets or sets a value indicating whether a printer identifier may name any printer, including one on a print
	/// server the user hasn't connected to (<c>windows:\\server\printer</c>). Defaults to <see langword="false"/>:
	/// a name with a backslash or a comma is looked up only if Windows lists it, as a local printer or as a
	/// connection the user already made.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Looking up <c>\\server\printer</c> contacts that server, and Windows may sign in to it with the
	/// application's account, sending a response that can be relayed or cracked. A comma opens special spooler
	/// handles, such as <c>name,Job 5</c>. When identifiers can come from outside the application, such as from a
	/// web request, allowing any name would let others make it contact any host.
	/// </para>
	/// <para>
	/// Turn this on to look up printers on servers that aren't connected yet, when every identifier the application
	/// looks up is trusted. Plain local printer names work either way.
	/// </para>
	/// </remarks>
	public bool AllowAnyPrinterName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to send PDF jobs straight to the printer over IPP when Latiprimere knows
	/// the queue's IPP address. Defaults to <see langword="false"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A PDF sent over IPP reaches the printer as it is, instead of as a picture of each page drawn through the
	/// driver: it prints faster, with sharp text, and the job reports how it really ended. A job goes this way only
	/// when all of these hold, and otherwise through the Windows queue as before:
	/// </para>
	/// <list type="bullet">
	/// <item><description>the queue's IPP URI is in <see cref="IppPrinterUris"/>, or the queue was added by URL, so
	/// its port is named after the printer's address;</description></item>
	/// <item><description>the queue isn't on a print server;</description></item>
	/// <item><description>the printer says it accepts PDF, which is asked at its first PDF job;</description></item>
	/// <item><description>the job doesn't set <see cref="PrintOptions.OutputFile"/>, an
	/// <see cref="PrintOptions.InputTray"/>, or a <c>windows:</c> vendor option.</description></item>
	/// </list>
	/// <para>
	/// Such jobs bypass the Windows queue: they don't appear in its queue window, and its pause, permissions,
	/// pooling and accounting don't apply to them. Their job IDs are the printer's, and <c>ipp:</c> vendor options
	/// are sent with them. If the printer can't be reached, the job goes through the Windows queue, which holds it
	/// until the printer is back.
	/// </para>
	/// </remarks>
	public bool PrintPdfOverIpp { get; set; }

	/// <summary>
	/// Gets the IPP URIs of Windows queues, by queue name (not case-sensitive), for <see cref="PrintPdfOverIpp"/>.
	/// </summary>
	/// <remarks>
	/// Needed for queues whose port doesn't give the printer's address, such as WSD and Standard TCP/IP ports, as
	/// Latiprimere doesn't guess it. Each URI must be an absolute <c>ipp</c>, <c>ipps</c>, <c>http</c> or
	/// <c>https</c> URI, such as <c>ipp://192.168.1.20/ipp/print</c>; the print system refuses others when created.
	/// </remarks>
	public IDictionary<string, Uri> IppPrinterUris { get; } = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets or sets the highest resolution, in dots per inch, at which PDF pages are rendered before they are sent to
	/// the printer, or <see langword="null"/> to render at the printer's own resolution. Defaults to
	/// <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// On Windows, PDF pages are rendered to images and drawn through the printer driver. A printer at 600 DPI gets
	/// 600 DPI images, which take time and memory. Below its resolution, pages are rendered at this value and
	/// scaled up to the printer's, so jobs take less time and memory, but small text and fine lines are softer.
	/// Printing 100 A4 pages to Microsoft Print to PDF at 600 DPI took 66 seconds and 225 MiB at full resolution,
	/// and 36 seconds and 130 MiB capped at 300 DPI.
	/// </para>
	/// <para>
	/// A value at or above the printer's resolution changes nothing. Raw jobs aren't rendered, so this doesn't
	/// affect them.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
	public int? MaxRenderDpi
	{
		get
		{
			return this.maxRenderDpi;
		}

		set
		{
			// A page needs at least one dot per inch to be rendered at all
			if (value is not null)
			{
				ArgumentOutOfRangeException.ThrowIfLessThan(value.Value, 1, nameof(WindowsOptions.MaxRenderDpi));
			}

			this.maxRenderDpi = value;
		}
	}
}
