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
}
