using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Latiprimere;
using Latiprimere.Cups;
using Latiprimere.Ipp;
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
	/// Lists printers without dependency injection, logging what the catalog does.
	/// </summary>
	/// <returns>A task that completes when the printers are listed.</returns>
	public static async Task ListWithoutDependencyInjectionAsync()
	{
		using ILoggerFactory loggerFactory = LoggerFactory.Create(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));

		using IPrintSystem cups = CupsPrintSystemFactory.Create();
		using IPrintSystem windows = WindowsPrintSystemFactory.Create();
		using IPrintSystem network = IppNetworkPrintSystemFactory.Create();

		// Only the catalog logs here: the print systems log only when created through dependency injection
		PrinterCatalog catalog = new PrinterCatalog([cups, windows, network], loggerFactory.CreateLogger<PrinterCatalog>());

		IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync();

		foreach (PrinterInfo printer in printers)
		{
			Console.WriteLine($"{printer.Name}  ({printer.Id})");
		}
	}
}
