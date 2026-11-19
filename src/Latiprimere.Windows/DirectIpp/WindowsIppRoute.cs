using System.Collections.Concurrent;
using Latiprimere.Ipp;
using Latiprimere.Ipp.Discovery;
using Latiprimere.Windows.Resources;
using Latiprimere.Windows.Spooler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Latiprimere.Windows.DirectIpp;

/// <summary>
/// Sends PDF jobs for Windows queues straight to their printers over IPP, when the queue's IPP URI is known exactly
/// (ADR 0027), or its WSD port's printer UUID matches a printer announced over mDNS (ADR 0036).
/// </summary>
internal sealed class WindowsIppRoute : IDisposable
{
	/// <summary>The prefix of a Windows printer name on a print server.</summary>
	private const string PrintServerPrefix = @"\\";

	/// <summary>How long a UUID that no printer announced isn't searched for again.</summary>
	internal static readonly TimeSpan UnmatchedRetryAfter = TimeSpan.FromMinutes(5);

	private readonly Dictionary<string, Uri> configuredUris; // From WindowsOptions.IppPrinterUris, by queue name
	private readonly IIppPrinterDiscovery? discovery;        // Finds printers by UUID; null turns matching off
	private readonly ILogger logger;                         // Where to log a failed search
	private readonly ConcurrentDictionary<string, Uri> matchedUris =
		new ConcurrentDictionary<string, Uri>(StringComparer.Ordinal); // Printers found by UUID, by UUID
	private readonly ConcurrentDictionary<string, DateTimeOffset> unmatchedUntil =
		new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal); // UUIDs not found, until when

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsIppRoute"/> class.
	/// </summary>
	/// <param name="context">The IPP client and settings routed jobs use. The route disposes its client.</param>
	/// <param name="configuredUris">IPP URIs for queue names, from <see cref="WindowsOptions.IppPrinterUris"/>.</param>
	/// <param name="discovery">Finds the printers of WSD queues by UUID over mDNS, or <see langword="null"/> to use
	/// only exactly known URIs.</param>
	/// <param name="logger">Where to log a failed search, or <see langword="null"/>.</param>
	/// <exception cref="ArgumentException">A configured URI isn't an absolute <c>ipp</c>, <c>ipps</c>, <c>http</c>
	/// or <c>https</c> URI.</exception>
	public WindowsIppRoute
	(
		IppPrintContext                          context,
		IEnumerable<KeyValuePair<string, Uri>>   configuredUris,
		IIppPrinterDiscovery?                    discovery = null,
		ILogger?                                 logger = null
	)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(configuredUris);

		this.Context = context;
		Dictionary<string, Uri> uris = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

		foreach (KeyValuePair<string, Uri> configured in configuredUris)
		{
			uris[configured.Key] = WindowsIppRoute.ToIppUri(configured.Value)
				?? throw new ArgumentException
				(
					ExceptionMessages.FormatIppPrinterUriInvalid(configured.Key, configured.Value),
					nameof(configuredUris)
				);
		}

		this.configuredUris = uris;
		this.discovery = discovery;
		this.logger = logger ?? NullLogger.Instance;
	}

	/// <summary>
	/// Gets how long to search the network for a WSD queue's printer. The search stops as soon as it answers.
	/// </summary>
	internal TimeSpan DiscoveryTimeout { get; init; } = IppNetworkOptions.DefaultDiscoveryDuration;

	/// <summary>
	/// Gets the IPP client and settings routed jobs use.
	/// </summary>
	public IppPrintContext Context { get; }

	/// <summary>
	/// Finds the IPP URI of a queue: configured for its name, or its port's name when that is the printer's URL.
	/// </summary>
	/// <param name="printerName">The queue's name.</param>
	/// <param name="portName">The queue's port name, or <see langword="null"/> if not known.</param>
	/// <returns>The URI, or <see langword="null"/> if the queue's printer can't be reached directly.</returns>
	/// <remarks>
	/// Nothing is guessed: hosts behind TCP/IP or WSD ports aren't probed. A queue on a print server is never routed,
	/// as its printer may not be reachable from this computer.
	/// </remarks>
	public Uri? FindPrinterUri(string printerName, string? portName)
	{
		ArgumentNullException.ThrowIfNull(printerName);

		if (printerName.StartsWith(WindowsIppRoute.PrintServerPrefix, StringComparison.Ordinal))
		{
			return null;
		}

		if (this.configuredUris.TryGetValue(printerName, out Uri? configured))
		{
			return configured;
		}

		// A queue added by URL is named after it, through the Internet Port or IPP port monitor
		return portName is not null && Uri.TryCreate(portName, UriKind.Absolute, out Uri? portUri)
			? WindowsIppRoute.ToIppUri(portUri)
			: null;
	}

	/// <summary>
	/// Finds where a queue's PDF jobs can go over IPP: its exactly known URI, or else, for a WSD queue, the printer
	/// announcing the UUID its port records.
	/// </summary>
	/// <param name="printerName">The queue's name.</param>
	/// <param name="portName">The queue's port name, or <see langword="null"/> if not known.</param>
	/// <param name="spool">Reads the WSD port's printer UUID.</param>
	/// <param name="cancellationToken">A token to cancel the search.</param>
	/// <returns>The target, or <see langword="null"/> if the queue's printer can't be reached directly.</returns>
	/// <remarks>
	/// Only a printer that announced the queue's UUID is contacted, so nothing is guessed (ADR 0024). A UUID found is
	/// remembered until <see cref="ForgetMatch"/>; one not found isn't searched for again for
	/// <see cref="UnmatchedRetryAfter"/>, so a queue whose printer doesn't speak IPP costs one search, not one a job.
	/// </remarks>
	public async Task<WindowsIppTarget?> FindTargetAsync(string printerName, string? portName, IWinSpool spool, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(printerName);
		ArgumentNullException.ThrowIfNull(spool);

		Uri? exact = this.FindPrinterUri(printerName, portName);

		if (exact is not null)
		{
			return new WindowsIppTarget { PrinterUri = exact };
		}

		if (this.discovery is null || portName is null || printerName.StartsWith(WindowsIppRoute.PrintServerPrefix, StringComparison.Ordinal))
		{
			return null;
		}

		string? uuid = DnsSdServiceMapper.NormalizeUuid(spool.GetWsdPrinterUuid(portName));

		if (uuid is null)
		{
			return null;
		}

		Uri? matched = await this.FindByUuidAsync(uuid, cancellationToken).ConfigureAwait(false);

		return matched is null ? null : new WindowsIppTarget { PrinterUri = matched, Uuid = uuid };
	}

	/// <summary>
	/// Forgets where the printer with a UUID was found, because it didn't answer there, or another printer did, so
	/// the next job searches for it again.
	/// </summary>
	/// <param name="uuid">The normalised UUID.</param>
	public void ForgetMatch(string uuid)
	{
		ArgumentNullException.ThrowIfNull(uuid);

		this.matchedUris.TryRemove(uuid, out _);
	}

	/// <summary>
	/// Releases the IPP client's connections.
	/// </summary>
	public void Dispose()
	{
		(this.Context.Client as IDisposable)?.Dispose();
	}

	/// <summary>
	/// Turns a printer URL into an IPP URI: <c>http</c> becomes <c>ipp</c> and <c>https</c> becomes <c>ipps</c>,
	/// keeping the port, which for those schemes is 80 or 443 when not given.
	/// </summary>
	/// <param name="uri">The URL.</param>
	/// <returns>The IPP URI, or <see langword="null"/> if the URL isn't absolute or has another scheme.</returns>
	internal static Uri? ToIppUri(Uri uri)
	{
		ArgumentNullException.ThrowIfNull(uri);

		if (!uri.IsAbsoluteUri || string.IsNullOrEmpty(uri.Host))
		{
			return null;
		}

		string? scheme = uri.Scheme switch
		{
			"ipp" or "http"   => "ipp",
			"ipps" or "https" => "ipps",
			_                 => null,
		};

		if (scheme is null)
		{
			return null;
		}

		// .NET knows http's and https's default ports but not ipp's, so an http URL without a port must keep 80
		UriBuilder builder = new UriBuilder(uri) { Scheme = scheme, Port = uri.Port };

		return builder.Uri;
	}

	/// <summary>
	/// Finds the printer announcing a UUID, from what was remembered or by searching the network.
	/// </summary>
	/// <param name="uuid">The normalised UUID.</param>
	/// <param name="cancellationToken">A token to cancel the search.</param>
	/// <returns>Its URI, or <see langword="null"/> if it wasn't found, now or recently.</returns>
	private async Task<Uri?> FindByUuidAsync(string uuid, CancellationToken cancellationToken)
	{
		if (this.matchedUris.TryGetValue(uuid, out Uri? known))
		{
			return known;
		}

		DateTimeOffset now = this.Context.TimeProvider.GetUtcNow();

		if (this.unmatchedUntil.TryGetValue(uuid, out DateTimeOffset until) && now < until)
		{
			return null;
		}

		DiscoveredIppPrinter? found;

		try
		{
			found = await this.discovery!.FindByUuidAsync(uuid, this.DiscoveryTimeout, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Multicast unavailable, for one: the queue's driver prints the job instead, as before matching existed
			WindowsLog.IppPrinterSearchFailed(this.logger, exception);
			found = null;
		}

		if (found is null)
		{
			this.unmatchedUntil[uuid] = now + WindowsIppRoute.UnmatchedRetryAfter;

			return null;
		}

		this.unmatchedUntil.TryRemove(uuid, out _);
		this.matchedUris[uuid] = found.PrinterUri;

		return found.PrinterUri;
	}
}
