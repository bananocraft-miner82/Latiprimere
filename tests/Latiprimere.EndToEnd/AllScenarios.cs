using Latiprimere.EndToEnd.Scenarios;

namespace Latiprimere.EndToEnd;

/// <summary>
/// Every scenario, and ways to choose some of them.
/// </summary>
public static class AllScenarios
{
	/// <summary>
	/// Gets every scenario, in the order they run.
	/// </summary>
	public static IReadOnlyList<Scenario> List { get; } =
	[
		CatalogScenarios.ListAcrossPrintSystems,
		CatalogFailureScenarios.OneSystemDown,
		CatalogFailureScenarios.EverySystemDown,
		CatalogFailureScenarios.NoPrintSystem,
		CatalogFailureScenarios.UnknownIds,
		SubmissionScenarios.CupsRawSameBytes,
		SubmissionScenarios.CupsPdfOutput,
		VirtualIppScenarios.CupsVendorOptions,
		VirtualIppScenarios.DirectOptions,
		LifecycleScenarios.CupsCancelWaitingJob,
		LifecycleScenarios.WindowsCancelWaitingJob,
		RenderingScenarios.MicrosoftPrintToPdfOutput,
		RenderingScenarios.TextOnlyDriverRefused,
		OutputScenarios.CupsPaper,
		OutputScenarios.CupsColourMode,
		OutputScenarios.WindowsPaperAndOrientation,
		OutputScenarios.WindowsCopies,
		OutputScenarios.WindowsColourMode,
		OutputScenarios.WindowsPrintTicket,
		IppSubmissionScenarios.OptionsArrive,
		IppSubmissionScenarios.TrayAndSize,
		IppSubmissionScenarios.RawBytes,
		IppSubmissionScenarios.UnsupportedValueRefused,
		IppRasterScenarios.PwgRasterPages,
		IppRasterScenarios.AppleRasterTwoSided,
		IppRasterScenarios.BusyRendersAgain,
		IppRasterScenarios.NoRasterizerRefused,
		IppCapabilitiesScenarios.HpEnvyProfile,
		VendorOptionScenarios.IppListedAndSent,
		VendorOptionScenarios.WindowsListedAndSent,
		DiscoveryScenarios.Mdns,
		IppLifecycleScenarios.StatesInOrder,
		IppLifecycleScenarios.AbortedWithReason,
		IppLifecycleScenarios.StoppedPrinter,
		IppLifecycleScenarios.CancelWaitingJob,
		IppFaultScenarios.BusyThenAccepts,
		IppFaultScenarios.BusyPastLimit,
		IppFaultScenarios.DroppedConnection,
		IppFaultScenarios.SlowPrinter,
		IppFaultScenarios.SwitchedOff,
		IppFaultScenarios.DualStack,
		IppSecurityScenarios.PasswordRequired,
		IppSecurityScenarios.UntrustedCertificate,
		WindowsIppRouteScenarios.PdfToPrinter,
		WindowsIppRouteScenarios.RasterToPrinter,
		WindowsIppFallbackScenarios.NoPdf,
		WindowsIppFallbackScenarios.Unreachable,
		WindowsIppFallbackScenarios.PasswordRequired,
		WindowsIppFallbackScenarios.RefusalThrown,
		ExampleScenarios.Listing,
		ExampleScenarios.Capabilities,
		ExampleScenarios.FirstPrintJob,
		ExampleScenarios.PrintOptionsAndJobs,
		ExampleScenarios.LabelsAndReceipts,
		ExampleScenarios.Errors,
		ExampleScenarios.PrintToPdf,
		WebServiceScenarios.WebService,
		CliScenarios.AgainstFakePrinter,
		ManualScenarios.PrinterDetails,
		ManualScenarios.TestPage,
		ManualScenarios.PaperAndColour,
		ManualScenarios.TwoSidedAndCopies,
		ManualScenarios.Landscape,
		ManualScenarios.Tray,
		ManualScenarios.WindowsQueueOverIpp
	];

	/// <summary>
	/// Finds a scenario by its ID.
	/// </summary>
	/// <param name="id">The ID, in any case.</param>
	/// <returns>The scenario.</returns>
	/// <exception cref="ArgumentException">No scenario has that ID.</exception>
	public static Scenario Find(string id)
	{
		return AllScenarios.List.FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.OrdinalIgnoreCase))
			?? throw new ArgumentException($"There is no scenario '{id}'.", nameof(id));
	}

	/// <summary>
	/// Chooses scenarios by ID, ID prefix or area. With nothing given, every scenario is chosen.
	/// </summary>
	/// <param name="filters">IDs (such as <c>cups.raw.same-bytes</c>), ID prefixes ending in <c>*</c> (such as
	/// <c>cups.*</c>) or area names (such as <c>Lifecycle</c>), in any case. A scenario matching any one is
	/// chosen.</param>
	/// <returns>The chosen scenarios, in the order they run.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="filters"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">A filter matches no scenario.</exception>
	public static IReadOnlyList<Scenario> Select(IReadOnlyCollection<string> filters)
	{
		ArgumentNullException.ThrowIfNull(filters);

		if (filters.Count == 0)
		{
			return AllScenarios.List;
		}

		foreach (string filter in filters)
		{
			if (!AllScenarios.List.Any(scenario => AllScenarios.Matches(scenario, filter)))
			{
				throw new ArgumentException($"No scenario matches '{filter}'.", nameof(filters));
			}
		}

		return AllScenarios.List.Where(scenario => filters.Any(filter => AllScenarios.Matches(scenario, filter))).ToList();
	}

	/// <summary>
	/// Says whether a scenario matches a filter.
	/// </summary>
	/// <param name="scenario">The scenario.</param>
	/// <param name="filter">An ID, an ID prefix ending in <c>*</c>, or an area name.</param>
	/// <returns><see langword="true"/> if it matches.</returns>
	private static bool Matches(Scenario scenario, string filter)
	{
		if (filter.EndsWith('*'))
		{
			return scenario.Id.StartsWith(filter[..^1], StringComparison.OrdinalIgnoreCase);
		}

		return string.Equals(scenario.Id, filter, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(scenario.Area.ToString(), filter, StringComparison.OrdinalIgnoreCase);
	}
}
