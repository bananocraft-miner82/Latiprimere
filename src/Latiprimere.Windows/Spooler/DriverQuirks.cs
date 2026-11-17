namespace Latiprimere.Windows.Spooler;

/// <summary>
/// Printer drivers that misbehave with some work, without saying so up front, so Latiprimere refuses that work
/// before anything is queued.
/// </summary>
/// <remarks>
/// Drivers are matched by driver name, which users can't rename, unlike printer names. Add a driver here only after
/// seeing it fail, and record how in the progress log.
/// </remarks>
internal static class DriverQuirks
{
	/// <summary>
	/// Drivers that accept a raw job and report it finished, but drop its data.
	/// </summary>
	/// <remarks>
	/// Microsoft Print To PDF: with an output file set, the job completes and the file is empty (seen 2026-11-13). The
	/// Microsoft XPS Document Writer, by contrast, passes raw data through unchanged.
	/// </remarks>
	private static readonly HashSet<string> RawDataDroppers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Microsoft Print To PDF",
	};

	/// <summary>
	/// Drivers that print text only, so can't print the page images Latiprimere prints a PDF as.
	/// </summary>
	/// <remarks>
	/// Generic / Text Only: a three-page PDF to a queue on a file port went from printing to an error, and held every
	/// job behind it until it was deleted (seen 2026-11-16, end-to-end harness). Each such job also spooled about
	/// 400 MB of page images.
	/// </remarks>
	private static readonly HashSet<string> TextOnlyDrivers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Generic / Text Only",
	};

	/// <summary>
	/// Decides whether a driver silently drops raw data.
	/// </summary>
	/// <param name="driverName">The printer's driver name, or <see langword="null"/> if unknown.</param>
	/// <returns>Whether raw jobs to the driver should be refused.</returns>
	internal static bool DropsRawData(string? driverName)
	{
		return driverName is not null && DriverQuirks.RawDataDroppers.Contains(driverName);
	}

	/// <summary>
	/// Decides whether a driver prints text only, and so can't print a PDF.
	/// </summary>
	/// <param name="driverName">The printer's driver name, or <see langword="null"/> if unknown.</param>
	/// <returns>Whether PDF jobs through the spooler to the driver should be refused.</returns>
	internal static bool PrintsTextOnly(string? driverName)
	{
		return driverName is not null && DriverQuirks.TextOnlyDrivers.Contains(driverName);
	}
}
