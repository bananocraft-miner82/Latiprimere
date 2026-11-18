namespace Latiprimere.Windows.Spooler;

/// <summary>
/// The Windows spooler operations Latiprimere uses, as blocking calls with managed results. Keeps the rest of the
/// adapter free of interop and lets tests substitute a fake spooler.
/// </summary>
/// <remarks>
/// Implementations translate spooler failures through <see cref="Mapping.WindowsErrorMapper"/>: a
/// <see cref="PrintException"/>, or a subclass of it, with the Win32 error code as
/// <see cref="PrintException.NativeErrorCode"/>; or <see cref="NotSupportedException"/> for a driver that refuses
/// raw data.
/// </remarks>
internal interface IWinSpool
{
	/// <summary>
	/// Lists the local printers and the user's printer connections.
	/// </summary>
	/// <returns>The printers.</returns>
	IReadOnlyList<WindowsPrinterRecord> EnumeratePrinters();

	/// <summary>
	/// Lists the names of the local printers and the user's printer connections, without contacting the print
	/// servers of those connections.
	/// </summary>
	/// <returns>The printer names.</returns>
	IReadOnlyList<string> EnumeratePrinterNames();

	/// <summary>
	/// Reads one printer's details.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <returns>The printer, or <see langword="null"/> if no printer has that name.</returns>
	WindowsPrinterRecord? GetPrinter(string printerName);

	/// <summary>
	/// Reads the current user's default printer name.
	/// </summary>
	/// <returns>The name, or <see langword="null"/> if no default printer is set.</returns>
	string? GetDefaultPrinterName();

	/// <summary>
	/// Sends data to a printer unchanged, as a RAW job.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="documentName">The name shown in the print queue.</param>
	/// <param name="content">The data to send, read from its current position to the end.</param>
	/// <param name="copies">How many times to send the data, in the one job. More than one needs a seekable
	/// <paramref name="content"/>, which is rewound to its starting position for each copy.</param>
	/// <param name="outputFile">A file to write to instead of the printer's port, or <see langword="null"/>.</param>
	/// <param name="cancellationToken">Checked between chunks of data; a cancelled job is removed from the queue.</param>
	/// <returns>The spooler's job ID.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	int SubmitRawJob
	(
		string            printerName,
		string            documentName,
		Stream            content,
		int               copies,
		string?           outputFile,
		CancellationToken cancellationToken
	);

	/// <summary>
	/// Reads a job's status.
	/// </summary>
	/// <param name="printerName">The printer the job was sent to.</param>
	/// <param name="jobId">The job ID.</param>
	/// <returns>The job, or <see langword="null"/> if the spooler no longer lists it.</returns>
	WindowsJobRecord? GetJob(string printerName, int jobId);

	/// <summary>
	/// Deletes (cancels) a job.
	/// </summary>
	/// <param name="printerName">The printer the job was sent to.</param>
	/// <param name="jobId">The job ID.</param>
	/// <returns><see langword="true"/> if the job was deleted; <see langword="false"/> if it no longer
	/// exists.</returns>
	bool DeleteJob(string printerName, int jobId);

	/// <summary>
	/// Reads a printer driver's capabilities and defaults.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="includePrintSchema">Whether to read the driver's Print Capabilities and default Print Ticket as
	/// well, which opens a Print Ticket session (ADR 0032).</param>
	/// <returns>The capabilities, or <see langword="null"/> if no printer has that name.</returns>
	WindowsCapabilitiesRecord? GetCapabilities(string printerName, bool includePrintSchema);

	/// <summary>
	/// Reads the driver's default settings as a full Print Ticket (ADR 0032).
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <returns>The ticket, as UTF-8 XML.</returns>
	/// <exception cref="PrintException">The Print Ticket API failed, or isn't available for this printer.</exception>
	/// <exception cref="PrinterNotFoundException">No printer has that name.</exception>
	byte[] GetDefaultPrintTicket(string printerName);

	/// <summary>
	/// Reads the driver's Print Capabilities, for a starting DEVMODE with Print Tickets merged on (ADR 0032).
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="baseDevMode">The DEVMODE to start from, as a job's saved <c>windows:devmode</c>; or
	/// <see langword="null"/> for the driver's defaults.</param>
	/// <param name="printTickets">Tickets, as UTF-8 XML, whose choices constrain the options; may be empty.</param>
	/// <returns>The Print Capabilities document, as UTF-8 XML.</returns>
	/// <exception cref="ArgumentException">The driver finds a ticket malformed.</exception>
	/// <exception cref="PrintException">The Print Ticket API failed, or isn't available for this printer.</exception>
	/// <exception cref="PrinterNotFoundException">No printer has that name.</exception>
	byte[] GetPrintCapabilities(string printerName, byte[]? baseDevMode, IReadOnlyList<byte[]> printTickets);

	/// <summary>
	/// Reads the printer UUID a WSD port records, which is the printer's IPP <c>printer-uuid</c> too (ADR 0036).
	/// </summary>
	/// <param name="portName">The port's name, such as <c>WSD-adb74ae8-…</c>.</param>
	/// <returns>The UUID as recorded, or <see langword="null"/> if the port isn't a WSD port or records none.</returns>
	string? GetWsdPrinterUuid(string portName);
}
