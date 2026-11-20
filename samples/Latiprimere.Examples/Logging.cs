using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Latiprimere;
using Latiprimere.Cups;
using Latiprimere.Ipp;
using Latiprimere.Rendering.Pdfium;
using Latiprimere.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Logging".
/// </summary>
internal static class Logging
{
	/// <summary>
	/// Sets Latiprimere up with console logging: everything Latiprimere logs, from Debug up, and only warnings from
	/// everything else.
	/// </summary>
	/// <returns>The service provider. Dispose it to flush the console logger.</returns>
	public static ServiceProvider BuildServiceProvider()
	{
		ServiceCollection services = new ServiceCollection();

		services.AddLogging
		(
			logging => logging
				.AddConsole()
				.SetMinimumLevel(LogLevel.Warning)
				.AddFilter("Latiprimere", LogLevel.Debug) // Every category that starts with "Latiprimere"
		);

		services.AddLatiprimere();
		services.AddLatiprimereCups();
		services.AddLatiprimereWindows();
		services.AddLatiprimerePdfium();
		services.AddLatiprimereIpp();

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Lists printers without dependency injection, logging what the catalog and the print systems do.
	/// </summary>
	/// <returns>A task that completes when the printers are listed.</returns>
	public static async Task ListWithoutDependencyInjectionAsync()
	{
		using ILoggerFactory loggerFactory = LoggerFactory.Create(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));

		// Each print system logs through the logger factory it is given
		using IPrintSystem cups = CupsPrintSystemFactory.Create(options: null, loggerFactory);
		using IPrintSystem windows = WindowsPrintSystemFactory.Create(new PdfiumRasterizer(), options: null, loggerFactory);
		using IPrintSystem network = IppNetworkPrintSystemFactory.Create(new PdfiumRasterizer(), options: null, loggerFactory);

		// And the catalog through its logger
		PrinterCatalog catalog = new PrinterCatalog([cups, windows, network], loggerFactory.CreateLogger<PrinterCatalog>());

		IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync();

		foreach (PrinterInfo printer in printers)
		{
			Console.WriteLine($"{printer.Name}  ({printer.Id})");
		}
	}
}
