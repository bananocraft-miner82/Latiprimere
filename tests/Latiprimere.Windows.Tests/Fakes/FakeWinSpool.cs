using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows.Tests.Fakes;

/// <summary>
/// An in-memory spooler with configurable printers and jobs, recording what is submitted and deleted.
/// </summary>
internal sealed class FakeWinSpool : IWinSpool
{
	private readonly List<WindowsPrinterRecord> printers = new List<WindowsPrinterRecord>(); // Listed printers
	private readonly Dictionary<int, WindowsJobRecord?> jobs = new Dictionary<int, WindowsJobRecord?>(); // null = gone
	private readonly List<int> deletedJobIds = new List<int>(); // Jobs deleted through DeleteJob
	private readonly Dictionary<string, WindowsCapabilitiesRecord> capabilities =
		new Dictionary<string, WindowsCapabilitiesRecord>(StringComparer.OrdinalIgnoreCase); // By printer name

	/// <summary>Gets or sets the default printer's name, or <see langword="null"/> for none.</summary>
	public string? DefaultPrinterName { get; set; }

	/// <summary>Gets or sets the job ID returned by the next submission.</summary>
	public int NextJobId { get; set; } = 1;

	/// <summary>Gets the printer name of the last submission.</summary>
	public string? LastSubmittedPrinterName { get; private set; }

	/// <summary>Gets the document name of the last submission.</summary>
	public string? LastSubmittedDocumentName { get; private set; }

	/// <summary>Gets the bytes of the last submission.</summary>
	public byte[]? LastSubmittedBytes { get; private set; }

	/// <summary>Gets the output file of the last submission, if any.</summary>
	public string? LastSubmittedOutputFile { get; private set; }

	/// <summary>Gets how many times the full printer details were listed, which contacts connections' servers.</summary>
	public int FullEnumerationCount { get; private set; }

	/// <summary>Gets every name GetPrinter was asked to open, as OpenPrinter would be, in order.</summary>
	public List<string> OpenedPrinterNames { get; } = new List<string>();

	/// <summary>Gets the printer UUIDs WSD ports record, by port name.</summary>
	public Dictionary<string, string> WsdPrinterUuids { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>Gets the WSD ports whose UUID was read, in order.</summary>
	public List<string> WsdPortsRead { get; } = new List<string>();

	/// <inheritdoc/>
	public string? GetWsdPrinterUuid(string portName)
	{
		this.WsdPortsRead.Add(portName);

		return this.WsdPrinterUuids.GetValueOrDefault(portName);
	}

	/// <summary>Gets the number of copies of the last raw job, or 0 before the first.</summary>
	public int LastSubmittedCopies { get; private set; }

	/// <summary>Gets the cancellation token the last raw job was submitted with.</summary>
	public CancellationToken LastSubmittedCancellationToken { get; private set; }

	/// <summary>Gets the jobs deleted through <see cref="DeleteJob"/>.</summary>
	public IReadOnlyList<int> DeletedJobIds
	{
		get
		{
			return this.deletedJobIds;
		}
	}

	/// <summary>Adds a printer to the spooler.</summary>
	/// <param name="printer">The printer.</param>
	public void AddPrinter(WindowsPrinterRecord printer)
	{
		this.printers.Add(printer);
	}

	/// <summary>Sets what the spooler reports for a job; <see langword="null"/> means it has left the queue.</summary>
	/// <param name="jobId">The job ID.</param>
	/// <param name="job">The job record, or <see langword="null"/>.</param>
	public void SetJob(int jobId, WindowsJobRecord? job)
	{
		this.jobs[jobId] = job;
	}

	/// <summary>Returns the printers added.</summary>
	/// <returns>The printers.</returns>
	public IReadOnlyList<WindowsPrinterRecord> EnumeratePrinters()
	{
		this.FullEnumerationCount++;

		return this.printers.ToList();
	}

	/// <summary>Returns the names of the printers added.</summary>
	/// <returns>The names.</returns>
	public IReadOnlyList<string> EnumeratePrinterNames()
	{
		return this.printers.Select(printer => printer.Name).ToList();
	}

	/// <summary>Returns the printer with the given name, ignoring case as Windows does.</summary>
	/// <param name="printerName">The printer name.</param>
	/// <returns>The printer, or <see langword="null"/>.</returns>
	public WindowsPrinterRecord? GetPrinter(string printerName)
	{
		this.OpenedPrinterNames.Add(printerName);

		return this.printers.FirstOrDefault
		(
			printer => string.Equals(printer.Name, printerName, StringComparison.OrdinalIgnoreCase)
		);
	}

	/// <summary>Returns the configured default printer name.</summary>
	/// <returns>The name, or <see langword="null"/>.</returns>
	public string? GetDefaultPrinterName()
	{
		return this.DefaultPrinterName;
	}

	/// <summary>Records the submission and returns <see cref="NextJobId"/>.</summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="documentName">The document name.</param>
	/// <param name="content">The data, read to the end.</param>
	/// <param name="copies">The number of copies.</param>
	/// <param name="outputFile">The output file, or <see langword="null"/>.</param>
	/// <param name="cancellationToken">Recorded, then checked before anything is recorded.</param>
	/// <returns>The job ID.</returns>
	/// <exception cref="PrintException">The printer does not exist.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	public int SubmitRawJob
	(
		string            printerName,
		string            documentName,
		Stream            content,
		int               copies,
		string?           outputFile,
		CancellationToken cancellationToken
	)
	{
		this.LastSubmittedCancellationToken = cancellationToken;
		cancellationToken.ThrowIfCancellationRequested();
		this.LastSubmittedCopies = copies;

		if (this.GetPrinter(printerName) is null)
		{
			throw new PrintException("Invalid printer name.") { NativeErrorCode = 1801 };
		}

		using MemoryStream copy = new MemoryStream();
		content.CopyTo(copy);
		this.LastSubmittedPrinterName = printerName;
		this.LastSubmittedDocumentName = documentName;
		this.LastSubmittedBytes = copy.ToArray();
		this.LastSubmittedOutputFile = outputFile;

		return this.NextJobId;
	}

	/// <summary>Returns the job set with <see cref="SetJob(int, WindowsJobRecord?)"/>, or null if never set.</summary>
	/// <param name="printerName">Not used.</param>
	/// <param name="jobId">The job ID.</param>
	/// <returns>The job, or <see langword="null"/>.</returns>
	public WindowsJobRecord? GetJob(string printerName, int jobId)
	{
		return this.jobs.GetValueOrDefault(jobId);
	}

	/// <summary>Deletes a job that is still listed; returns false for one that has left the queue.</summary>
	/// <param name="printerName">Not used.</param>
	/// <param name="jobId">The job ID.</param>
	/// <returns>Whether the job was deleted.</returns>
	public bool DeleteJob(string printerName, int jobId)
	{
		if (this.jobs.GetValueOrDefault(jobId) is null)
		{
			return false;
		}

		this.deletedJobIds.Add(jobId);
		this.jobs[jobId] = null;

		return true;
	}

	/// <summary>
	/// Sets the capabilities reported for a printer.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="record">The capabilities.</param>
	public void SetCapabilities(string printerName, WindowsCapabilitiesRecord record)
	{
		this.capabilities[printerName] = record;
	}

	/// <summary>Returns the capabilities set for the printer, without the Print Capabilities unless asked for, as
	/// the real spooler does.</summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="includePrintSchema">Whether the Print Capabilities were asked for, which is recorded.</param>
	/// <returns>The capabilities, or <see langword="null"/> if none were set.</returns>
	public WindowsCapabilitiesRecord? GetCapabilities(string printerName, bool includePrintSchema)
	{
		this.PrintSchemaRequests += includePrintSchema ? 1 : 0;
		WindowsCapabilitiesRecord? record = this.capabilities.GetValueOrDefault(printerName);

		return includePrintSchema || record is null
			? record
			: record with { PrintCapabilities = null, DefaultPrintTicket = null, PrintSchemaFailure = null };
	}

	/// <summary>Gets how many capability reads asked for the Print Capabilities too.</summary>
	public int PrintSchemaRequests { get; private set; }

	/// <summary>Gets or sets the default Print Ticket every printer reports, as UTF-8 XML.</summary>
	public byte[] DefaultPrintTicket { get; set; } = [];

	/// <summary>Gets or sets the Print Capabilities document every printer reports, as UTF-8 XML.</summary>
	public byte[] PrintCapabilities { get; set; } = [];

	/// <summary>Gets or sets an exception the Print Ticket calls throw, as for a driver without Print Ticket
	/// support.</summary>
	public Exception? PrintTicketFailure { get; set; }

	/// <summary>Gets the tickets the last <see cref="GetPrintCapabilities"/> call was given.</summary>
	public IReadOnlyList<byte[]>? LastPrintCapabilitiesTickets { get; private set; }

	/// <summary>Gets the starting DEVMODE the last <see cref="GetPrintCapabilities"/> call was given.</summary>
	public byte[]? LastPrintCapabilitiesDevMode { get; private set; }

	/// <summary>Returns <see cref="DefaultPrintTicket"/>.</summary>
	/// <param name="printerName">The printer name.</param>
	/// <returns>The ticket.</returns>
	public byte[] GetDefaultPrintTicket(string printerName)
	{
		return this.PrintTicketFailure is null ? this.DefaultPrintTicket : throw this.PrintTicketFailure;
	}

	/// <summary>Returns <see cref="PrintCapabilities"/>, recording the tickets.</summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="baseDevMode">The starting DEVMODE, which is recorded.</param>
	/// <param name="printTickets">The tickets.</param>
	/// <returns>The document.</returns>
	public byte[] GetPrintCapabilities(string printerName, byte[]? baseDevMode, IReadOnlyList<byte[]> printTickets)
	{
		this.LastPrintCapabilitiesTickets = printTickets;
		this.LastPrintCapabilitiesDevMode = baseDevMode;

		return this.PrintTicketFailure is null ? this.PrintCapabilities : throw this.PrintTicketFailure;
	}
}
