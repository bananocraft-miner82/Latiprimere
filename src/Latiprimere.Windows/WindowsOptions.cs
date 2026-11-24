using System.Net;
using System.Net.Security;
using Latiprimere.Ipp;

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
	/// or can find the queue's IPP printer. Defaults to <see langword="false"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A PDF sent over IPP reaches the printer as it is, instead of as a picture of each page drawn through the
	/// driver: it prints faster, with sharp text, and the job reports how it really ended. A job goes this way only
	/// when all of these hold, and otherwise through the Windows queue as before:
	/// </para>
	/// <list type="bullet">
	/// <item><description>the queue's IPP URI is in <see cref="IppPrinterUris"/>; or the queue was added by URL, so
	/// its port is named after the printer's address; or it is on a WSD port, and a printer on the network announces
	/// itself over mDNS with the UUID the port records, and reports that UUID when asked;</description></item>
	/// <item><description>the queue isn't on a print server;</description></item>
	/// <item><description>the printer says it accepts PDF, or, with a PDF rasteriser, a raster format in
	/// <see cref="IppRasterFormats"/>, which is asked at its first PDF job. A PDF it can't take is rendered and sent in
	/// the first of those it takes, at the printer's resolution limited by <see cref="MaxRenderDpi"/>;</description></item>
	/// <item><description>the job doesn't set <see cref="PrintOptions.OutputFile"/>, an
	/// <see cref="PrintOptions.InputTray"/>, or a <c>windows:</c> vendor option.</description></item>
	/// </list>
	/// <para>
	/// Such jobs bypass the Windows queue: they don't appear in its queue window, and its pause, permissions,
	/// pooling and accounting don't apply to them. Their job IDs are the printer's, and <c>ipp:</c> vendor options
	/// are sent with them. If the printer can't be reached, the job goes through the Windows queue, which holds it
	/// until the printer is back. Finding a WSD queue's printer listens on the network for up to two seconds at its
	/// first PDF job; a printer not found isn't searched for again for five minutes.
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
	/// Gets the SHA-256 fingerprints of printer certificates to trust when <see cref="PrintPdfOverIpp"/> sends a job
	/// over IPPS (<c>ipps://</c>), in hexadecimal, with or without colons between the bytes, such as <c>3A:7F:…</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Printers nearly always present a certificate they made themselves, which no computer trusts, so until its
	/// certificate is trusted a queue whose printer is reached over IPPS prints through the Windows queue instead. The
	/// fingerprint is logged when that happens; check it against the one the printer's web page shows before adding
	/// it. They work as <c>IppNetworkOptions.TrustedCertificateFingerprints</c> do for printing to network printers
	/// directly.
	/// </para>
	/// <para>
	/// Once a certificate is pinned, or <see cref="IppServerCertificateValidationCallback"/> is set, a WSD queue's
	/// printer that announces an IPPS address as well is reached over IPPS, and over plain IPP only if its certificate
	/// isn't trusted.
	/// </para>
	/// </remarks>
	public IList<string> IppTrustedCertificateFingerprints { get; } = new List<string>();

	/// <summary>
	/// Gets the SHA-256 fingerprints of printer certificates to trust when <see cref="PrintPdfOverIpp"/> sends a job
	/// over IPPS, only at one printer's address, by printer URI, such as <c>ipps://192.168.1.20/ipp/print</c>. They
	/// are matched by the URI's host, and work as <c>IppNetworkOptions.PrinterCertificateFingerprints</c> do.
	/// </summary>
	/// <remarks>
	/// A certificate in <see cref="IppTrustedCertificateFingerprints"/> is trusted at any address, which matters when
	/// printers of one model share a certificate made at the factory. One pinned here is refused anywhere else.
	/// </remarks>
	public IDictionary<Uri, string> IppPrinterCertificateFingerprints { get; } = new Dictionary<Uri, string>();

	/// <summary>
	/// Gets or sets the check, for <see cref="PrintPdfOverIpp"/>, of a printer's certificate that neither the system
	/// trusts nor <see cref="IppTrustedCertificateFingerprints"/> pins; or <see langword="null"/>, the default, to
	/// refuse it.
	/// </summary>
	/// <remarks>
	/// Return <see langword="true"/> only for a certificate you have checked, for example by its fingerprint: anyone
	/// able to intercept the connection could otherwise read the documents and any password.
	/// </remarks>
	public RemoteCertificateValidationCallback? IppServerCertificateValidationCallback { get; set; }

	/// <summary>
	/// Gets or sets the user name and password <see cref="PrintPdfOverIpp"/> sends to every printer over IPPS, or
	/// <see langword="null"/>, the default, to send none. <see cref="IppPrinterCredentials"/> overrides it for
	/// particular printers.
	/// </summary>
	/// <remarks>
	/// <para>
	/// They are sent with every request, as HTTP Basic authentication, and only over IPPS, never in clear text. The
	/// <see cref="NetworkCredential.Domain"/> is not used. A queue whose printer asks for a password it won't be sent,
	/// as one reached over plain IPP does, prints through the Windows queue instead.
	/// </para>
	/// <para>
	/// While they are set, a certificate the system trusts is enough only for printers in <see cref="IppPrinterUris"/>
	/// or <see cref="IppPrinterCredentials"/>: anyone on the network can announce a printer with a WSD queue's UUID at
	/// a host whose certificate a public authority issued, so a printer found that way must have its certificate
	/// pinned in <see cref="IppTrustedCertificateFingerprints"/> or accepted by
	/// <see cref="IppServerCertificateValidationCallback"/>. To keep a password to one printer, use
	/// <see cref="IppPrinterCredentials"/>.
	/// </para>
	/// </remarks>
	public NetworkCredential? IppCredentials { get; set; }

	/// <summary>
	/// Gets the user names and passwords of particular printers for <see cref="PrintPdfOverIpp"/>, by printer URI,
	/// such as <c>ipps://192.168.1.20/ipp/print</c>. They are matched by host and port, and sent only over IPPS, as
	/// <see cref="IppCredentials"/> are.
	/// </summary>
	public IDictionary<Uri, NetworkCredential> IppPrinterCredentials { get; } = new Dictionary<Uri, NetworkCredential>();

	/// <summary>
	/// Gets the raster formats <see cref="PrintPdfOverIpp"/> may render a PDF into for a printer that can't take PDF,
	/// in the order they're tried: by default PWG raster, then Apple raster, then PCLm, as
	/// <c>IppNetworkOptions.RasterFormats</c> has them. Empty, such a printer's PDF jobs print through its driver.
	/// </summary>
	public IList<IppRasterFormat> IppRasterFormats { get; } = new List<IppRasterFormat>
	{
		IppRasterFormat.PwgRaster,
		IppRasterFormat.AppleRaster,
		IppRasterFormat.Pclm
	};

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
