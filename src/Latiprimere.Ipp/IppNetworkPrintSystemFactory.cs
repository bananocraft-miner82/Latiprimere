using Latiprimere.Ipp.Discovery;
using Microsoft.Extensions.Logging;

namespace Latiprimere.Ipp;

/// <summary>
/// Creates the IPP network print system, for applications that don't use dependency injection.
/// </summary>
public static class IppNetworkPrintSystemFactory
{
	/// <summary>
	/// Creates a print system that prints directly to IPP printers on the network.
	/// </summary>
	/// <param name="options">The configuration, or <see langword="null"/> for discovery only.</param>
	/// <returns>
	/// The print system. It holds an HTTP connection, so dispose it, for example with <c>using</c>, when it
	/// is no longer needed.
	/// </returns>
	public static IPrintSystem Create(IppNetworkOptions? options = null)
	{
		return IppNetworkPrintSystemFactory.Create(options, loggerFactory: null);
	}

	/// <summary>
	/// Creates the direct IPP print system, logging through a logger factory when there is one.
	/// </summary>
	/// <param name="options">How to find and reach printers, or <see langword="null"/> for the defaults.</param>
	/// <param name="loggerFactory">The application's logger factory, or <see langword="null"/> to log nothing.</param>
	/// <returns>The print system.</returns>
	internal static IPrintSystem Create(IppNetworkOptions? options, ILoggerFactory? loggerFactory)
	{
		options ??= new IppNetworkOptions();

		SharpIppClientAdapter client = new SharpIppClientAdapter(IppHttpClientFactory.CreateForTcp(options.ConnectTimeout));

		IppPrintContext context = new IppPrintContext
		{
			Client             = client,
			RequestingUserName = options.UserName ?? Environment.UserName,
			DocumentFormats    = IppDocumentFormats.Standard,
			// Network printers that take one job at a time turn others away; CUPS queues them instead
			BusyRetryDuration  = options.BusyRetryDuration,
			Logger             = IppLog.CreateLogger(loggerFactory)
		};

		return new IppNetworkPrintSystem(context, new MdnsIppPrinterDiscovery(), options, client);
	}
}
