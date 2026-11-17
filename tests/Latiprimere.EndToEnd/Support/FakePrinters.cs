using Latiprimere.FakeIppPrinter;
using Latiprimere.Ipp;
using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// Starts scripted IPP printers for scenarios, and builds catalogs that reach them over direct IPP.
/// </summary>
public static class FakePrinters
{
	/// <summary>
	/// Starts a fake printer and notes where it is.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="options">How to set it up, or <see langword="null"/> for an IPP Everywhere printer like
	/// <c>ippeveprinter</c>.</param>
	/// <returns>The running printer. The caller disposes it.</returns>
	public static async Task<FakePrinter> StartAsync(ScenarioContext context, FakePrinterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(context);

		FakePrinter printer = await FakePrinter.StartAsync(options, context.CancellationToken);
		context.Note($"Fake printer ({printer.Profile.Name}) at {printer.PrinterUri}");

		return printer;
	}

	/// <summary>
	/// Builds a catalog over direct IPP with only this printer configured and mDNS discovery off.
	/// </summary>
	/// <param name="printerUri">The printer's URI.</param>
	/// <param name="configure">Changes other IPP options, or <see langword="null"/>.</param>
	/// <returns>The catalog fixture. The caller disposes it.</returns>
	public static CatalogFixture CreateCatalog(Uri printerUri, Action<IppNetworkOptions>? configure = null)
	{
		return new CatalogFixture
		(
			services => services.AddLatiprimereIpp
			(
				options =>
				{
					options.EnableDiscovery = false;
					options.PrinterUris.Add(printerUri);
					configure?.Invoke(options);
				}
			)
		);
	}

	/// <summary>
	/// Gets the printer ID direct IPP gives a fake printer.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>The printer ID.</returns>
	public static string GetPrinterId(FakePrinter printer)
	{
		ArgumentNullException.ThrowIfNull(printer);

		return TestPrinters.GetIppPrinterId(printer.PrinterUri);
	}

	/// <summary>
	/// Keeps every request the printer received as evidence, with all their attributes.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="printer">The printer.</param>
	/// <returns>A task that completes when the file is written.</returns>
	public static Task SaveRequestsAsync(ScenarioContext context, FakePrinter printer)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(printer);

		List<string> lines = new List<string>();

		foreach (RecordedRequest request in printer.Requests)
		{
			lines.Add($"{request.ReceivedAt:HH:mm:ss.fff} {request}");
			lines.AddRange(request.OperationAttributes.Select(attribute => $"    operation: {attribute}"));
			lines.AddRange(request.JobAttributes.Select(attribute => $"    job:       {attribute}"));
		}

		return context.SaveEvidenceAsync("requests.txt", lines, "Every request the fake printer received");
	}
}
