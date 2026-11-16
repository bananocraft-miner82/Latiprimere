using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Latiprimere;
using Latiprimere.Cups;
using Latiprimere.Ipp;
using Latiprimere.Rendering.Pdfium;
using Latiprimere.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Installation": setting Latiprimere up with and without dependency injection.
/// </summary>
internal static class Installation
{
	/// <summary>
	/// Registers Latiprimere and every print system with dependency injection.
	/// </summary>
	/// <returns>The service provider, which the caller disposes.</returns>
	public static ServiceProvider BuildServiceProvider()
	{
		ServiceCollection services = new ServiceCollection();

		services.AddLatiprimere();         // The printer catalog
		services.AddLatiprimereCups();     // Linux and macOS
		services.AddLatiprimereWindows();  // Windows
		services.AddLatiprimerePdfium();   // PDFs on Windows
		services.AddLatiprimereIpp();      // Network printers, on every platform

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Creates the print systems and the catalog directly, without dependency injection, and lists the printers.
	/// </summary>
	/// <returns>A task that completes when the printers have been listed.</returns>
	public static async Task ListWithoutDependencyInjectionAsync()
	{
		using IPrintSystem cups = CupsPrintSystemFactory.Create();
		using IPrintSystem windows = WindowsPrintSystemFactory.Create(new PdfiumRasterizer());
		using IPrintSystem network = IppNetworkPrintSystemFactory.Create();

		PrinterCatalog catalog = new PrinterCatalog([cups, windows, network]);

		IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync();

		foreach (PrinterInfo printer in printers)
		{
			Console.WriteLine($"{printer.Name}  ({printer.Id})");
		}
	}
}
