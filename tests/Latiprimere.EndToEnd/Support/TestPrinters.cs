namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// The IDs of the virtual printers the scenarios print to. Scenarios never print to any other printer: the catalog
/// lists real printers too, and those must not receive test jobs.
/// </summary>
public static class TestPrinters
{
	/// <summary>
	/// The test container's CUPS-PDF queue, which renders jobs to PDF files in <c>pdf/</c> of its output folder.
	/// </summary>
	public const string CupsPdf = "cups:CUPS-PDF";

	/// <summary>
	/// The CUPS Raw queue, which saves job data unchanged.
	/// </summary>
	public const string CupsRaw = "cups:Raw";

	/// <summary>
	/// The CUPS Paused queue, which is disabled so jobs wait until cancelled.
	/// </summary>
	public const string CupsPaused = "cups:Paused";

	/// <summary>
	/// The CUPS IPP Everywhere queue in front of <c>ippeveprinter</c>.
	/// </summary>
	public const string CupsVirtualIpp = "cups:Virtual-IPP";

	/// <summary>
	/// The Windows test printer that writes each job's bytes to <c>Latiprimere-Raw.prn</c>.
	/// </summary>
	public const string WindowsRaw = "windows:Latiprimere Raw";

	/// <summary>
	/// The Windows test printer that keeps finished jobs in its queue.
	/// </summary>
	public const string WindowsRetained = "windows:Latiprimere Retained";

	/// <summary>
	/// The paused Windows test printer, where jobs wait until cancelled.
	/// </summary>
	public const string WindowsPaused = "windows:Latiprimere Paused";

	/// <summary>
	/// Microsoft Print to PDF's name in Windows.
	/// </summary>
	public const string MicrosoftPrintToPdfName = "Microsoft Print to PDF";

	/// <summary>
	/// Microsoft Print to PDF's printer ID.
	/// </summary>
	public const string MicrosoftPrintToPdf = "windows:" + TestPrinters.MicrosoftPrintToPdfName;

	/// <summary>
	/// Gets the printer ID direct IPP gives a printer URI.
	/// </summary>
	/// <param name="printerUri">The printer's URI.</param>
	/// <returns>The printer ID.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="printerUri"/> is <see langword="null"/>.</exception>
	public static string GetIppPrinterId(Uri printerUri)
	{
		ArgumentNullException.ThrowIfNull(printerUri);

		return "ipp:" + printerUri.AbsoluteUri;
	}
}
