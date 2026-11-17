using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Finding network printers by their mDNS announcements (DNS-SD), as an application with direct IPP does.
/// </summary>
public static class DiscoveryScenarios
{
	/// <summary>
	/// A printer announcing itself over mDNS is listed, looked up and read by its name. Reads only, so it is safe
	/// against a real printer.
	/// </summary>
	public static readonly Scenario Mdns = new Scenario
	{
		Id = "ipp.discovery.mdns",
		Area = ScenarioArea.Discovery,
		Title = "A printer announcing itself over mDNS is listed as ipp:dnssd:<name>, and is looked up and its capabilities read by that ID (reads only, prints nothing)",
		Needs = TargetNeeds.MdnsPrinter,
		RunAsync = DiscoveryScenarios.MdnsAsync
	};

	/// <summary>
	/// Runs <see cref="Mdns"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task MdnsAsync(ScenarioContext context)
	{
		string name = context.Targets.MdnsPrinterName!; // Needed, so set
		string printerId = "ipp:dnssd:" + name;

		// Direct IPP as an application registers it: discovery on, no printers configured
		await using CatalogFixture fixture = new CatalogFixture(services => services.AddLatiprimereIpp());

		IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(context.CancellationToken);
		List<string> discovered = printers.Where(printer => printer.Id.StartsWith("ipp:dnssd:", StringComparison.Ordinal)).Select(printer => printer.Id).ToList();
		context.Note($"Discovered over mDNS: {(discovered.Count == 0 ? "nothing" : string.Join(", ", discovered))}");
		context.Check($"the listing has {printerId}", discovered.Contains(printerId));

		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, context.CancellationToken);
		context.CheckEqual("looking it up by that ID finds it", printerId, printer.Info.Id);
		context.Check("the printer reports its make and model", !string.IsNullOrWhiteSpace(printer.Info.MakeAndModel), printer.Info.MakeAndModel ?? "none");

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		context.Check("its capabilities can be read", capabilities.MediaSizes.Count > 0, $"{capabilities.MediaSizes.Count} paper sizes");
	}
}
