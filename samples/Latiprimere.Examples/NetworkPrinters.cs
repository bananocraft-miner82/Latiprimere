using System;
using System.IO;
using System.Threading.Tasks;
using Latiprimere;
using Microsoft.Extensions.DependencyInjection;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Network printers over IPP".
/// </summary>
internal static class NetworkPrinters
{
	/// <summary>
	/// The address of a printer that discovery can't find, because it is on another part of the network.
	/// </summary>
	private const string WarehousePrinterUri = "ipp://192.168.20.15/ipp/print";

	/// <summary>
	/// Registers direct IPP printing, with one printer configured by address as well as those discovery finds, and
	/// PDFium, so PDFs to printers that can't take PDF are sent as raster.
	/// </summary>
	/// <returns>The service provider, which the caller disposes.</returns>
	public static ServiceProvider BuildServiceProvider()
	{
		ServiceCollection services = new ServiceCollection();

		services.AddLatiprimere();
		services.AddLatiprimerePdfium(); // Renders PDFs for printers that can't take PDF
		services.AddLatiprimereIpp(options =>
		{
			// A printer that discovery can't see, such as one on another subnet
			options.PrinterUris.Add(new Uri(NetworkPrinters.WarehousePrinterUri));

			options.DiscoveryDuration = TimeSpan.FromSeconds(3); // Listen a little longer on a busy network
			options.ConnectTimeout = TimeSpan.FromSeconds(5);    // Give up sooner on printers that are off
			options.BusyRetryDuration = TimeSpan.FromMinutes(5); // A slow label printer can be busy for a while
		});

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Prints a label on the printer configured by address, using its URI identifier.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="label">The label, in the printer's own language.</param>
	/// <returns>A task that completes when the label has been sent.</returns>
	public static async Task PrintToConfiguredPrinterAsync(IPrinterCatalog catalog, Stream label)
	{
		IPrinter printer = await catalog.GetPrinterAsync("ipp:" + NetworkPrinters.WarehousePrinterUri);

		await printer.SubmitAsync(PrintContent.FromRaw(label), PrintOptions.Default);
	}
}
