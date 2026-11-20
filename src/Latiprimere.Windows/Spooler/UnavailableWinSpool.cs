using Latiprimere.Windows.Resources;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// Stands in for the spooler on platforms other than Windows. The print system reports itself as unsupported there,
/// so the printer catalog never calls it; direct calls throw <see cref="PlatformNotSupportedException"/>.
/// </summary>
internal sealed class UnavailableWinSpool : IWinSpool
{
	/// <inheritdoc/>
	/// <exception cref="PlatformNotSupportedException">Always.</exception>
	public IReadOnlyList<string> EnumeratePrinterNames()
	{
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	/// <exception cref="PlatformNotSupportedException">Always.</exception>
	public IReadOnlyList<WindowsPrinterRecord> EnumeratePrinters()
	{
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	/// <exception cref="PlatformNotSupportedException">Always.</exception>
	public WindowsPrinterRecord? GetPrinter(string printerName)
	{
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	/// <exception cref="PlatformNotSupportedException">Always.</exception>
	public string? GetDefaultPrinterName()
	{
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	/// <exception cref="PlatformNotSupportedException">Always.</exception>
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
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	/// <exception cref="PlatformNotSupportedException">Always.</exception>
	public WindowsJobRecord? GetJob(string printerName, int jobId)
	{
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	/// <exception cref="PlatformNotSupportedException">Always.</exception>
	public bool DeleteJob(string printerName, int jobId)
	{
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	public WindowsCapabilitiesRecord? GetCapabilities(string printerName, bool includePrintSchema)
	{
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	public byte[] GetDefaultPrintTicket(string printerName)
	{
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	public byte[] GetPrintCapabilities(string printerName, byte[]? baseDevMode, IReadOnlyList<byte[]> printTickets)
	{
		throw UnavailableWinSpool.CreateException();
	}

	/// <inheritdoc/>
	/// <remarks>Always <see langword="null"/>: there are no ports.</remarks>
	public string? GetWsdPrinterUuid(string portName)
	{
		return null;
	}

	/// <inheritdoc/>
	/// <remarks>Always <see langword="null"/>: there is no spooler to signal changes.</remarks>
	public IWindowsChangeNotification? OpenChangeNotification(string printerName, WindowsChanges changes)
	{
		return null;
	}

	/// <summary>
	/// Creates the exception thrown by every member but this and <see cref="GetWsdPrinterUuid"/>.
	/// </summary>
	/// <returns>The exception.</returns>
	private static PlatformNotSupportedException CreateException()
	{
		return new PlatformNotSupportedException(ExceptionMessages.WindowsOnly);
	}
}
