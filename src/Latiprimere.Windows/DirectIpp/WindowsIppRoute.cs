using Latiprimere.Ipp;
using Latiprimere.Windows.Resources;

namespace Latiprimere.Windows.DirectIpp;

/// <summary>
/// Sends PDF jobs for Windows queues straight to their printers over IPP, when the queue's IPP URI is known exactly
/// (ADR 0027).
/// </summary>
internal sealed class WindowsIppRoute : IDisposable
{
	/// <summary>The prefix of a Windows printer name on a print server.</summary>
	private const string PrintServerPrefix = @"\\";

	private readonly Dictionary<string, Uri> configuredUris; // From WindowsOptions.IppPrinterUris, by queue name

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsIppRoute"/> class.
	/// </summary>
	/// <param name="context">The IPP client and settings routed jobs use. The route disposes its client.</param>
	/// <param name="configuredUris">IPP URIs for queue names, from <see cref="WindowsOptions.IppPrinterUris"/>.</param>
	/// <exception cref="ArgumentException">A configured URI isn't an absolute <c>ipp</c>, <c>ipps</c>, <c>http</c>
	/// or <c>https</c> URI.</exception>
	public WindowsIppRoute(IppPrintContext context, IEnumerable<KeyValuePair<string, Uri>> configuredUris)
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
	}

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
}
