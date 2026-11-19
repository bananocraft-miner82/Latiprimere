using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
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
	/// A printer remembered in the address store is found there after a restart, without answering the search
	/// (ADR 0038).
	/// </summary>
	public static readonly Scenario RememberedAddress = new Scenario
	{
		Id = "ipp.discovery.remembered-address",
		Area = ScenarioArea.Discovery,
		Title = "A printer an earlier run remembered in AddressStorePath, now silent on mDNS, is found by name at its remembered address and prints; with another UUID remembered, it isn't used",
		Needs = TargetNeeds.None,
		RunAsync = DiscoveryScenarios.RememberedAddressAsync
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

	/// <summary>
	/// Runs <see cref="RememberedAddress"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task RememberedAddressAsync(ScenarioContext context)
	{
		const string name = "Latiprimere Remembered Printer"; // Announced by nothing, so only the store knows it
		const string printerId = "ipp:dnssd:" + name;

		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		string uuid = fake.Profile.GetValues("printer-uuid").Single();

		// As an earlier run left it: the printer's address and the UUID it reports
		string store = DiscoveryScenarios.WriteStore(context, "remembered.json", name, fake.PrinterUri, uuid);

		await using (CatalogFixture fixture = DiscoveryScenarios.CreateCatalog(store))
		{
			IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, context.CancellationToken);
			context.CheckEqual("it is found by name", printerId, printer.Info.Id);

			using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
			IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
			PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
			context.CheckEqual("and the job sent to its remembered address completes", PrintJobState.Completed, status.State);
		}

		// Another printer's UUID remembered at that address: the printer there isn't taken for it
		string wrong = DiscoveryScenarios.WriteStore(context, "wrong-uuid.json", name, fake.PrinterUri, "00000000-0000-0000-0000-000000000001");
		await using CatalogFixture wrongFixture = DiscoveryScenarios.CreateCatalog(wrong);
		PrinterNotFoundException? notFound = null;

		try
		{
			await wrongFixture.Catalog.GetPrinterAsync(printerId, context.CancellationToken);
		}
		catch (PrinterNotFoundException caught)
		{
			notFound = caught;
		}

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Check("with another UUID remembered, it isn't found", notFound is not null);
	}

	/// <summary>
	/// Writes an address store remembering one printer.
	/// </summary>
	/// <param name="context">The scenario's context, whose evidence folder holds the file.</param>
	/// <param name="fileName">The file's name.</param>
	/// <param name="name">The printer's name.</param>
	/// <param name="printerUri">Its address.</param>
	/// <param name="uuid">The UUID remembered for it.</param>
	/// <returns>The file's path.</returns>
	private static string WriteStore(ScenarioContext context, string fileName, string name, Uri printerUri, string uuid)
	{
		string path = context.GetEvidencePath(fileName);
		string json = $$"""
			{
			  "version": 1,
			  "printers": [
			    { "name": "{{name}}", "printerUri": "{{printerUri.AbsoluteUri}}", "uuid": "{{uuid}}" }
			  ]
			}
			""";

		File.WriteAllText(path, json);
		context.AddEvidence(path, "The address store, as an earlier run would leave it");

		return path;
	}

	/// <summary>
	/// Builds a catalog over direct IPP with an address store, searching the network only briefly.
	/// </summary>
	/// <param name="store">The address store's path.</param>
	/// <returns>The catalog fixture. The caller disposes it.</returns>
	private static CatalogFixture CreateCatalog(string store)
	{
		return new CatalogFixture
		(
			services => services.AddLatiprimereIpp
			(
				options =>
				{
					options.AddressStorePath = store;
					options.DiscoveryDuration = TimeSpan.FromSeconds(1);
				}
			)
		);
	}
}
