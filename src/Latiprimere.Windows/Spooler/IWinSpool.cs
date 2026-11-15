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
	/// <returns>The capabilities, or <see langword="null"/> if no printer has that name.</returns>
	WindowsCapabilitiesRecord? GetCapabilities(string printerName);
}
