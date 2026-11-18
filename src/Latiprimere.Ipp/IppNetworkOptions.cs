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

	private TimeSpan busyRetryDuration = IppNetworkOptions.DefaultBusyRetryDuration; // Backs BusyRetryDuration
	private TimeSpan connectTimeout = IppNetworkOptions.DefaultConnectTimeout;       // Backs ConnectTimeout
	private TimeSpan discoveryDuration = IppNetworkOptions.DefaultDiscoveryDuration; // Backs DiscoveryDuration
	private int? maxRenderDpi;                                                        // Backs MaxRenderDpi

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
	/// Gets or sets the user name sent with requests. When <see langword="null"/>, the current operating-system user
	/// name is used.
	/// </summary>
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
	/// Gets or sets the highest resolution, in dots per inch, a PDF is rendered at when it is sent as raster to a
	/// printer that can't take PDF. Defaults to <see langword="null"/>: the printer's default resolution.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A printer that doesn't take PDF is sent its pages as images, in PWG raster or Apple raster, at one of the
	/// resolutions it lists, which needs a PDF rasteriser such as Latiprimere.Rendering.Pdfium. Its default is used
	/// when it has one and it is within this limit; otherwise the highest it lists within the limit and 600 dpi.
	/// If it lists none that low, its lowest is used.
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
}
