using Latiprimere.EndToEnd.Support;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// The printer catalog over every print system at once.
/// </summary>
public static class CatalogScenarios
{
	/// <summary>
	/// One catalog over CUPS, direct IPP and the Windows spooler lists each test printer once and looks it up again.
	/// </summary>
	public static readonly Scenario ListAcrossPrintSystems = new Scenario
	{
		Id = "catalog.list-across-print-systems",
		Area = ScenarioArea.Catalog,
		Title = "One catalog over every print system here lists each test printer once under its print system's ID, and looks it up again by that ID",
		Needs = TargetNeeds.None,
		RunAsync = CatalogScenarios.ListAcrossPrintSystemsAsync
	};

	/// <summary>
	/// The prefixes of the print systems' printer IDs.
	/// </summary>
	private static readonly string[] IdPrefixes = ["cups:", "ipp:", "windows:"];

	/// <summary>
	/// Runs <see cref="ListAcrossPrintSystems"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task ListAcrossPrintSystemsAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(context.CancellationToken);
		context.Note($"The catalog listed {printers.Count} printers.");

		await context.SaveEvidenceAsync
		(
			"printers.txt",
			printers.Select(printer => $"{printer.Id} | {printer.Name} | {printer.State}" + (printer.IsDefault ? " | default" : string.Empty)),
			"Every printer the catalog listed"
		);

		// The catalog joins the print systems' lists: nothing twice, and every ID says which system it came from
		List<string> ids = printers.Select(printer => printer.Id).ToList();
		context.CheckEqual("every printer ID is listed once", ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

		List<string> unprefixed = ids.Where(id => !CatalogScenarios.IdPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))).ToList();
		context.Check("every printer ID starts with its print system's prefix", unprefixed.Count == 0, unprefixed.Count == 0 ? null : string.Join(", ", unprefixed));

		// Each test printer this machine has, from each print system, is there and routes back to the same printer
		foreach (string printerId in CatalogScenarios.GetExpectedTestPrinters(context.Targets))
		{
			if (context.Check($"lists {printerId}", ids.Contains(printerId)))
			{
				IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, context.CancellationToken);
				context.CheckEqual($"looks {printerId} up again by its ID", printerId, printer.Info.Id);
			}
		}

		foreach (string pausedId in CatalogScenarios.GetExpectedPausedPrinters(context.Targets))
		{
			PrinterInfo? paused = printers.FirstOrDefault(printer => printer.Id == pausedId);

			if (paused is not null)
			{
				context.CheckEqual($"reports {pausedId} as stopped", PrinterState.Stopped, paused.State);
			}
		}

		IPrinter? defaultPrinter = await fixture.Catalog.GetDefaultPrinterAsync(context.CancellationToken);
		string? defaultId = defaultPrinter?.Info.Id;
		context.Check("the default printer is one of those listed", defaultId is null || ids.Contains(defaultId), defaultId ?? "no default printer");
	}

	/// <summary>
	/// Gets the IDs of the test printers the targets say this machine has.
	/// </summary>
	/// <param name="targets">The targets.</param>
	/// <returns>The printer IDs.</returns>
	private static List<string> GetExpectedTestPrinters(HarnessTargets targets)
	{
		List<string> printerIds = new List<string>();

		if (targets.CupsServer is not null)
		{
			printerIds.AddRange([TestPrinters.CupsRaw, TestPrinters.CupsPaused, TestPrinters.CupsVirtualIpp]);

			if (targets.CupsSetup == CupsTestSetup.Container)
			{
				printerIds.Add(TestPrinters.CupsPdf);
			}
		}

		if (targets.IppPrinterUri is not null)
		{
			printerIds.Add(TestPrinters.GetIppPrinterId(targets.IppPrinterUri));
		}

		if (targets.Available.HasFlag(TargetNeeds.WindowsTestPrinters))
		{
			printerIds.AddRange([TestPrinters.WindowsRaw, TestPrinters.WindowsRetained, TestPrinters.WindowsPaused]);
		}

		if (targets.HasMicrosoftPrintToPdf)
		{
			printerIds.Add(TestPrinters.MicrosoftPrintToPdf);
		}

		return printerIds;
	}

	/// <summary>
	/// Gets the IDs of the paused test printers the targets say this machine has.
	/// </summary>
	/// <param name="targets">The targets.</param>
	/// <returns>The printer IDs.</returns>
	private static List<string> GetExpectedPausedPrinters(HarnessTargets targets)
	{
		List<string> printerIds = new List<string>();

		if (targets.CupsServer is not null)
		{
			printerIds.Add(TestPrinters.CupsPaused);
		}

		if (targets.Available.HasFlag(TargetNeeds.WindowsTestPrinters))
		{
			printerIds.Add(TestPrinters.WindowsPaused);
		}

		return printerIds;
	}
}
