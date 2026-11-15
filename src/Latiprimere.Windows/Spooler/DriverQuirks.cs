namespace Latiprimere.Windows.Spooler;

/// <summary>
/// Printer drivers that misbehave without reporting an error, so Latiprimere refuses the work up front instead.
/// </summary>
/// <remarks>
/// Drivers are matched by driver name, which users can't rename, unlike printer names. Add a driver here only after
/// seeing it fail silently, and record how in the progress log.
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
	/// Decides whether a driver silently drops raw data.
	/// </summary>
	/// <param name="driverName">The printer's driver name, or <see langword="null"/> if unknown.</param>
	/// <returns>Whether raw jobs to the driver should be refused.</returns>
	internal static bool DropsRawData(string? driverName)
	{
		return driverName is not null && DriverQuirks.RawDataDroppers.Contains(driverName);
	}
}
