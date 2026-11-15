using Latiprimere.Ipp.Discovery;

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
		options ??= new IppNetworkOptions();

		SharpIppClientAdapter client = new SharpIppClientAdapter(IppHttpClientFactory.CreateForTcp(options.ConnectTimeout));

		IppPrintContext context = new IppPrintContext
		{
			Client             = client,
			RequestingUserName = options.UserName ?? Environment.UserName,
			DocumentFormats    = IppDocumentFormats.Standard,
			// Network printers that take one job at a time turn others away; CUPS queues them instead
			BusyRetryDuration  = options.BusyRetryDuration
		};

		return new IppNetworkPrintSystem(context, new MdnsIppPrinterDiscovery(), options, client);
	}
}
