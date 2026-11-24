using System.Net;
using System.Net.Security;

namespace Latiprimere.Ipp;

/// <summary>
/// Configures printing directly to IPP network printers, without CUPS or the Windows spooler.
/// </summary>
public sealed class IppNetworkOptions
{
	/// <summary>
	/// The default time spent listening for printer announcements.
	/// </summary>
	public static readonly TimeSpan DefaultDiscoveryDuration = TimeSpan.FromSeconds(2);

	/// <summary>
	/// The default time spent resending a job to a printer that is busy with another.
	/// </summary>
	public static readonly TimeSpan DefaultBusyRetryDuration = TimeSpan.FromMinutes(2);

	/// <summary>
	/// The default longest wait for a connection to a printer to open.
	/// </summary>
	public static readonly TimeSpan DefaultConnectTimeout = IppHttpClientFactory.DefaultConnectTimeout;

	/// <summary>
	/// The default longest a printer may go without taking any more of a job's document, or without answering once it
	/// has been sent it all: 5 minutes.
	/// </summary>
	public static readonly TimeSpan DefaultUploadStallTimeout = TimeSpan.FromMinutes(5);

	private TimeSpan busyRetryDuration = IppNetworkOptions.DefaultBusyRetryDuration;   // Backs BusyRetryDuration
	private TimeSpan connectTimeout = IppNetworkOptions.DefaultConnectTimeout;         // Backs ConnectTimeout
	private TimeSpan discoveryDuration = IppNetworkOptions.DefaultDiscoveryDuration;   // Backs DiscoveryDuration
	private TimeSpan uploadStallTimeout = IppNetworkOptions.DefaultUploadStallTimeout; // Backs UploadStallTimeout
	private int? maxRenderDpi;                                                          // Backs MaxRenderDpi

	/// <summary>
	/// Gets or sets a value indicating whether to find printers on the local network with DNS-SD (Bonjour/mDNS)
	/// when listing printers. Defaults to <see langword="true"/>. Looking up a printer by its name identifier
	/// (<c>ipp:dnssd:</c>) always searches the network, whatever this setting.
	/// </summary>
	public bool EnableDiscovery { get; set; } = true;

	/// <summary>
	/// Gets or sets how long to listen for printer announcements each time printers are listed. A lookup by name
	/// that has to search the network waits at most this long, and usually much less, as it stops when the printer
	/// answers; it only searches when the printer's last known address doesn't answer.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The value is negative, or is longer than about 24 days.</exception>
	public TimeSpan DiscoveryDuration
	{
		get
		{
			return this.discoveryDuration;
		}
		set
		{
			// Task.Delay, which times the listening, refuses negative times and anything longer
			ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMilliseconds(int.MaxValue));

			this.discoveryDuration = value;
		}
	}

	/// <summary>
	/// Gets the URIs of printers to list even if they don't announce themselves, such as
	/// <c>ipp://192.168.1.20/ipp/print</c>.
	/// </summary>
	public IList<Uri> PrinterUris { get; } = new List<Uri>();

	/// <summary>
	/// Gets or sets a value indicating whether a printer identifier may name any IPP URI
	/// (<c>ipp:ipp://host/path</c>), not only one in <see cref="PrinterUris"/> or of a printer found on the network.
	/// Defaults to <see langword="false"/>.
	/// </summary>
	/// <remarks>
	/// Looking a printer up by URI connects to whatever host and port the URI names. When identifiers can come from
	/// outside the application, such as from a web request, allowing any URI would let others make it connect to
	/// internal services (server-side request forgery). Turn this on only when every identifier the application
	/// looks up is trusted. Otherwise a lookup of an unlisted URI finds no printer, without connecting.
	/// </remarks>
	public bool AllowAnyPrinterUri { get; set; }

	/// <summary>
	/// Gets or sets the longest wait for a connection to a printer to open. Defaults to
	/// <see cref="DefaultConnectTimeout"/>, 10 seconds; without a limit, an address with nothing behind it can take
	/// about 21 seconds to fail on Windows.
	/// </summary>
	/// <remarks>
	/// It covers opening the connection only, not waiting for an answer. When it runs out, the request fails with a
	/// <see cref="PrintException"/>.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The value is not positive, or is longer than about 24 days.</exception>
	public TimeSpan ConnectTimeout
	{
		get
		{
			return this.connectTimeout;
		}

		set
		{
			IppHttpClientFactory.ValidateConnectTimeout(value);
			this.connectTimeout = value;
		}
	}

	/// <summary>
	/// Gets or sets the longest a printer may go without taking any more of a job's document, or without answering
	/// once it has been sent it all. Defaults to <see cref="DefaultUploadStallTimeout"/>, 5 minutes;
	/// <see cref="Timeout.InfiniteTimeSpan"/> waits as long as the printer takes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It limits a lack of progress, not the whole upload: a large job to a printer that reads it only as fast as it
	/// prints takes as long as it needs while the printer keeps reading, and the time spent rendering a PDF sent as
	/// raster isn't counted. A printer that stops reading partway, such as one that jams, sleeps or loses its network
	/// connection without closing it, would otherwise hold the submission until the caller's cancellation token, if
	/// any, cancels it.
	/// </para>
	/// <para>
	/// When it runs out, the submission fails with a <see cref="PrintException"/> saying whether the printer stopped
	/// taking the document or didn't answer once it was sent it all. The job isn't sent again: the printer may already
	/// have printed some of it. Allow for a printer that pauses before it starts, to warm up or clean its heads, and
	/// between pages, as an inkjet printing on both sides does while the ink dries.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The value is not positive, other than
	/// <see cref="Timeout.InfiniteTimeSpan"/>, or is longer than about 24 days.</exception>
	public TimeSpan UploadStallTimeout
	{
		get
		{
			return this.uploadStallTimeout;
		}

		set
		{
			IppHttpClientFactory.ValidateUploadStallTimeout(value);
			this.uploadStallTimeout = value;
		}
	}

	/// <summary>
	/// Gets or sets how long to keep resending a job that a printer turns away because it is busy with another
	/// (IPP status <c>server-error-busy</c>). Printers that take one job at a time do this. Defaults to
	/// <see cref="DefaultBusyRetryDuration"/>; <see cref="TimeSpan.Zero"/> sends each job once.
	/// </summary>
	/// <remarks>
	/// The waits between attempts start at one second and double, up to 15 seconds, as each attempt sends the whole
	/// document again. Only documents in seekable streams can be resent; others fail on the first busy answer. When
	/// the time runs out, the busy error is thrown as a <see cref="PrintException"/>.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
	public TimeSpan BusyRetryDuration
	{
		get
		{
			return this.busyRetryDuration;
		}

		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
			this.busyRetryDuration = value;
		}
	}

	/// <summary>
	/// Gets or sets the user name sent with requests as <c>requesting-user-name</c>, which printers list jobs under.
	/// When <see langword="null"/>, the current operating-system user name is used.
	/// </summary>
	/// <remarks>
	/// It isn't a login, and printers don't check it. For a printer that asks for a user name and password, set
	/// <see cref="Credentials"/> or <see cref="PrinterCredentials"/>.
	/// </remarks>
	public string? UserName { get; set; }

	/// <summary>
	/// Gets or sets whether to ask a printer to check a job's attributes with Validate-Job before sending the
	/// document. Defaults to <see langword="true"/>.
	/// </summary>
	/// <remarks>
	/// A printer refuses a job with an option it can't carry out, such as a paper size it doesn't have. With this on,
	/// it refuses before the document is uploaded, with a <see cref="PrintException"/> whose
	/// <see cref="PrintException.NativeErrorCode"/> is the IPP status, rather than after. It costs one short request
	/// per job, and is only sent to printers that list Validate-Job among their operations. A printer too busy to
	/// answer it gets the job anyway.
	/// </remarks>
	public bool ValidateJobs { get; set; } = true;

	/// <summary>
	/// Gets the raster formats a PDF may be rendered into for a printer that can't take PDF, in the order they're
	/// tried: by default PWG raster, then Apple raster, then PCLm.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The first the printer lists is used. A printer that takes PDF is always sent the PDF. Reorder the list to
	/// prefer a format, or leave one out so it's never sent, as for a printer that handles one badly. Empty, a printer
	/// that can't take PDF is refused PDFs with a <see cref="NotSupportedException"/>, as without a rasteriser.
	/// </para>
	/// <para>
	/// PCLm is a PDF of image strips, which Mopria printers take; Latiprimere compresses its strips with Flate or
	/// run-length encoding, whichever the printer prefers.
	/// </para>
	/// </remarks>
	public IList<IppRasterFormat> RasterFormats { get; } = new List<IppRasterFormat>
	{
		IppRasterFormat.PwgRaster,
		IppRasterFormat.AppleRaster,
		IppRasterFormat.Pclm
	};

	/// <summary>
	/// Gets or sets the highest resolution, in dots per inch, a PDF is rendered at when it is sent as raster to a
	/// printer that can't take PDF. Defaults to <see langword="null"/>: the printer's default resolution.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A printer that doesn't take PDF is sent its pages as images, in a format of <see cref="RasterFormats"/>, at one
	/// of the resolutions it lists, which needs a PDF rasteriser such as Latiprimere.Rendering.Pdfium. Its default is used
	/// when it has one and it is within this limit; otherwise the highest it lists within the limit and 600 dpi.
	/// If it lists none that low, its lowest is used. Resolutions above 2400 dpi are never used, whatever this limit.
	/// </para>
	/// <para>
	/// Lower resolutions render and upload faster: an A4 page at 600 dpi is four times the pixels of one at
	/// 300 dpi. Small text and fine lines are softer.
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
				ArgumentOutOfRangeException.ThrowIfLessThan(value.Value, 1, nameof(IppNetworkOptions.MaxRenderDpi));
			}

			this.maxRenderDpi = value;
		}
	}

	/// <summary>
	/// Gets the SHA-256 fingerprints of printer certificates to trust over IPPS (<c>ipps://</c>), in hexadecimal, with
	/// or without colons between the bytes, such as <c>3A:7F:…</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Printers nearly always present a certificate they made themselves, which no computer trusts, so a printer's
	/// IPPS address is refused until its certificate is trusted. A certificate pinned here is trusted whatever name it
	/// carries, as printers' certificates rarely name the address they're reached at. The
	/// <see cref="PrintException"/> for a refused certificate gives its fingerprint; check it against the one the
	/// printer's web page shows before adding it.
	/// </para>
	/// <para>
	/// Once a certificate is pinned, or <see cref="ServerCertificateValidationCallback"/> is set, a printer found on
	/// the network that announces both addresses is reached over IPPS, and over plain IPP only if its certificate
	/// isn't trusted and it has never been reached over IPPS before. Set <see cref="RequireEncryption"/> to never
	/// reach one over plain IPP.
	/// </para>
	/// <para>
	/// A pinned certificate is trusted at any address. Printers of one model sometimes share a certificate made at
	/// the factory, which would then be trusted on every one of them; to trust a certificate only at one printer's
	/// address, pin it in <see cref="PrinterCertificateFingerprints"/> instead.
	/// </para>
	/// </remarks>
	public IList<string> TrustedCertificateFingerprints { get; } = new List<string>();

	/// <summary>
	/// Gets the SHA-256 fingerprints of printer certificates to trust over IPPS only at one printer's address, by
	/// printer URI, such as <c>ipps://192.168.1.20/ipp/print</c>. They are matched by the URI's host, and written as
	/// in <see cref="TrustedCertificateFingerprints"/>.
	/// </summary>
	/// <remarks>
	/// A certificate pinned here is trusted only at that host, whatever name it carries, so the same certificate
	/// presented anywhere else is refused. A printer found on the network is reached at the address it announces, so
	/// pin it by that address; if it gets a new one, its certificate is refused there until it is pinned again.
	/// </remarks>
	public IDictionary<Uri, string> PrinterCertificateFingerprints { get; } = new Dictionary<Uri, string>();

	/// <summary>
	/// Gets or sets a value indicating whether every request goes over an encrypted connection (IPPS), never plain
	/// IPP. Defaults to <see langword="false"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <see langword="true"/>, a printer found on the network is reached only at its IPPS address: one that
	/// announces none, or whose certificate isn't trusted, is refused with a <see cref="PrintException"/> rather than
	/// reached over plain IPP, where anyone able to watch the network could read the documents. Looking up an
	/// <c>ipp://</c> URI is refused the same way, without connecting, and an <c>ipp://</c> URI in
	/// <see cref="PrinterUris"/> throws <see cref="ArgumentException"/> when the print system is created.
	/// </para>
	/// <para>
	/// Without it, a printer found on the network is still never reached over plain IPP once it has been reached over
	/// IPPS with a trusted certificate, while a certificate is pinned or the callback is set, so a device that
	/// answers in its place, or blocks its IPPS port, can't make it fall back; unless
	/// <see cref="AllowPlainIppAfterIpps"/> is set.
	/// </para>
	/// </remarks>
	public bool RequireEncryption { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether a printer found on the network may be reached over plain IPP after it
	/// has been reached over IPPS, when its IPPS address stops working. Defaults to <see langword="false"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// By default, once a printer found on the network has been reached over IPPS with a trusted certificate, while a
	/// certificate is pinned or <see cref="ServerCertificateValidationCallback"/> is set, it is refused with a
	/// <see cref="PrintException"/> rather than reached over plain IPP, if its IPPS address stops answering, its
	/// certificate is no longer trusted, or its name is announced with only a plain address. Any of those could be a
	/// device on the network forcing its jobs onto plain IPP, where they can be read. A printer that has never been
	/// reached over IPPS falls back either way.
	/// </para>
	/// <para>
	/// Set it to <see langword="true"/> to fall back as before, as for printers whose certificates change often and
	/// whose documents needn't be kept private. The fallback is logged as a warning. It has no effect while
	/// <see cref="RequireEncryption"/> is set, which never allows plain IPP. Whether each printer was reached over IPPS
	/// is remembered either way, so turning it off again protects them at once.
	/// </para>
	/// </remarks>
	public bool AllowPlainIppAfterIpps { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether a printer found on the network that announced a UUID may be used at its
	/// remembered address when the printer there reports no UUID at all. Defaults to <see langword="false"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Looking a printer up by name tries the address it was last seen at first, and uses it only if the printer there
	/// is the same: it must report, as <c>printer-uuid</c>, the UUID it announced. By default, one that reports no UUID
	/// isn't taken on trust, as another device at the address could pass by leaving it out; the network is searched
	/// for the name instead, which takes a little longer.
	/// </para>
	/// <para>
	/// Set it to <see langword="true"/> to take such a printer on trust, as before, for older printers that announce a
	/// UUID but don't report <c>printer-uuid</c> and that would otherwise be searched for at every lookup. A printer
	/// that reports a different UUID is never used there, and one that announced none is always taken on trust.
	/// </para>
	/// </remarks>
	public bool AllowRememberedAddressWithoutUuid { get; set; }

	/// <summary>
	/// Gets or sets the check of a printer's certificate that neither the system trusts nor
	/// <see cref="TrustedCertificateFingerprints"/> pins, or <see langword="null"/>, the default, to refuse it.
	/// </summary>
	/// <remarks>
	/// Return <see langword="true"/> only for a certificate you have checked, for example by its fingerprint: anyone
	/// able to intercept the connection could otherwise read the documents and any password.
	/// </remarks>
	public RemoteCertificateValidationCallback? ServerCertificateValidationCallback { get; set; }

	/// <summary>
	/// Gets or sets the user name and password sent to every printer over IPPS, or <see langword="null"/>, the default,
	/// to send none. <see cref="PrinterCredentials"/> overrides it for particular printers.
	/// </summary>
	/// <remarks>
	/// <para>
	/// They are sent with every request, as HTTP Basic authentication, and only over IPPS, never in clear text. A
	/// printer that asks for a password over plain IPP is refused with a <see cref="PrintAuthenticationException"/>
	/// saying to use its <c>ipps://</c> address. The <see cref="NetworkCredential.Domain"/> is not used.
	/// </para>
	/// <para>
	/// Every printer reached over IPPS receives them, once its certificate is trusted. While they are set, a
	/// certificate the system trusts is enough only for printers configured in <see cref="PrinterUris"/> or
	/// <see cref="PrinterCredentials"/>: anyone on the network can announce a printer at a host whose certificate a
	/// public authority issued, so a printer found on the network must have its certificate pinned in
	/// <see cref="TrustedCertificateFingerprints"/> or accepted by <see cref="ServerCertificateValidationCallback"/>.
	/// Until then its IPPS address is refused, and it is reached over plain IPP, without them, if it announces that
	/// too and <see cref="RequireEncryption"/> isn't set. To keep a password to one printer, use
	/// <see cref="PrinterCredentials"/>.
	/// </para>
	/// </remarks>
	public NetworkCredential? Credentials { get; set; }

	/// <summary>
	/// Gets the user names and passwords of particular printers, by printer URI, such as
	/// <c>ipps://192.168.1.20/ipp/print</c>. They are matched by host and port, and sent only over IPPS, as
	/// <see cref="Credentials"/> are.
	/// </summary>
	public IDictionary<Uri, NetworkCredential> PrinterCredentials { get; } = new Dictionary<Uri, NetworkCredential>();

	/// <summary>
	/// Gets or sets a file in which to remember where each printer found on the network was last seen, so it is
	/// found there after the application restarts even if it doesn't answer the search, as a sleeping printer may
	/// not. Defaults to <see langword="null"/>: remembered only while the print system lives.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The file is JSON, read when the print system is created and written whenever a printer's address changes. A
	/// missing or unreadable file means starting with nothing remembered, and is logged, never an error; so is a
	/// file that can't be written. A remembered address is used only while the printer there still reports the
	/// UUID it announced.
	/// </para>
	/// <para>
	/// Keep it where only the application can write: an address in it counts as a printer found on the network, so
	/// it may be looked up by its <c>ipp:</c> URI identifier as well as by name.
	/// </para>
	/// </remarks>
	public string? AddressStorePath { get; set; }
}
