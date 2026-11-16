using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Mapping;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows;

/// <summary>
/// The Windows print spooler: lists the user's printers and prints to them.
/// </summary>
/// <remarks>
/// Printer identifiers are the spooler's printer name prefixed with <c>windows:</c>, so they never clash with
/// other print systems.
/// </remarks>
internal sealed class WindowsPrintSystem : IPrintSystem
{
	/// <summary>The prefix of every printer identifier from this print system.</summary>
	internal const string PrinterIdPrefix = "windows:";

	private readonly WindowsPrintContext context; // Spooler, GDI, rasteriser and clock

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsPrintSystem"/> class.
	/// </summary>
	/// <param name="context">The spooler, GDI, rasteriser and clock.</param>
	/// <param name="isSupported">Whether the spooler is available on this platform.</param>
	public WindowsPrintSystem(WindowsPrintContext context, bool isSupported)
	{
		ArgumentNullException.ThrowIfNull(context);

		this.context = context;
		this.IsSupported = isSupported;
	}

	/// <inheritdoc/>
	public string Name
	{
		get
		{
			return "Windows";
		}
	}

	/// <inheritdoc/>
	public bool IsSupported { get; }

	/// <summary>
	/// Gets a value indicating whether a printer name may name any printer (<see cref="WindowsOptions.AllowAnyPrinterName"/>).
	/// </summary>
	internal bool AllowsAnyPrinterName
	{
		get
		{
			return this.context.AllowsAnyPrinterName;
		}
	}

	/// <summary>
	/// Gets the route for PDF jobs straight to IPP printers, or <see langword="null"/> when it is off, so the
	/// settings can be checked.
	/// </summary>
	internal DirectIpp.WindowsIppRoute? IppRoute
	{
		get
		{
			return this.context.IppRoute;
		}
	}

	/// <summary>
	/// Gets the GDI printer that draws rendered PDF pages, so its settings can be checked.
	/// </summary>
	internal IGdiPrinter Gdi
	{
		get
		{
			return this.context.Gdi;
		}
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
	{
		// Spooler calls block, so run them off the caller's thread
		return Task.Run(this.ListPrinters, cancellationToken);
	}

	/// <inheritdoc/>
	public Task<IPrinter?> FindPrinterAsync(string printerId, CancellationToken cancellationToken = default)
	{
		// Identifiers from other print systems are not ours to look up
		ArgumentNullException.ThrowIfNull(printerId);

		if (!printerId.StartsWith(WindowsPrintSystem.PrinterIdPrefix, StringComparison.Ordinal)
			  || printerId.Length == WindowsPrintSystem.PrinterIdPrefix.Length)
		{
			return Task.FromResult<IPrinter?>(null);
		}

		string printerName = printerId[WindowsPrintSystem.PrinterIdPrefix.Length..];

		return Task.Run<IPrinter?>(() => this.FindPrinter(printerName), cancellationToken);
	}

	/// <inheritdoc/>
	public Task<IPrinter?> FindDefaultPrinterAsync(CancellationToken cancellationToken = default)
	{
		return Task.Run<IPrinter?>
		(
			() =>
			{
				string? defaultName = this.context.Spool.GetDefaultPrinterName();

				return defaultName is null ? null : this.FindPrinter(defaultName);
			},
			cancellationToken
		);
	}

	/// <summary>
	/// Lists the printers, flagging the default.
	/// </summary>
	/// <returns>The printers.</returns>
	private IReadOnlyList<PrinterInfo> ListPrinters()
	{
		string? defaultName = this.context.Spool.GetDefaultPrinterName();

		return this.context.Spool
			.EnumeratePrinters()
			.Select(
				printer => WindowsPrinterInfoMapper.ToPrinterInfo
				(
					printer,
					WindowsPrintSystem.PrinterIdPrefix + printer.Name,
					string.Equals(printer.Name, defaultName, StringComparison.OrdinalIgnoreCase)
				)
			)
			.ToList();
	}

	/// <summary>
	/// Releases the IPP connections used for PDF jobs sent straight to printers, if that is on. The spooler needs no
	/// connection held open: each call opens and closes its own handles.
	/// </summary>
	public void Dispose()
	{
		this.context.IppRoute?.Dispose();
	}

	/// <summary>
	/// Checks whether opening a printer name could do more than open a printer on this computer: a backslash names
	/// a print server (<c>\\server\printer</c>), and a comma opens special handles (<c>name,Job 5</c>,
	/// <c>,XcvMonitor Local Port</c>).
	/// </summary>
	/// <param name="printerName">The name from the printer identifier.</param>
	/// <returns>Whether the name must be listed before it is opened.</returns>
	private static bool CanReachBeyondThisComputer(string printerName)
	{
		return printerName.Contains('\\', StringComparison.Ordinal) || printerName.Contains(',', StringComparison.Ordinal);
	}

	/// <summary>
	/// Checks whether Windows lists a printer, local or a connection, by this name.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <returns>Whether it is listed.</returns>
	private bool IsListed(string printerName)
	{
		// Names only, which Windows reads locally: the full details would ask every connected print server, and one
		// that is offline would hold each lookup up for its timeout
		return this.context.Spool
			.EnumeratePrinterNames()
			.Contains(printerName, StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Looks up one printer by its spooler name.
	/// </summary>
	/// <param name="printerName">The spooler's name for the printer.</param>
	/// <returns>The printer, or <see langword="null"/> if the spooler has no printer with that name.</returns>
	private WindowsPrinter? FindPrinter(string printerName)
	{
		// A name with a backslash names a printer on another computer, which OpenPrinter would contact, signing in
		// with this process's Windows account. A comma opens special spooler handles. Unless the application
		// allows any name (WindowsOptions.AllowAnyPrinterName), such names are opened only when Windows already
		// lists them, so an identifier from outside the application can't make it connect to any host (ADR 0024)
		if (!this.context.AllowsAnyPrinterName
			&& WindowsPrintSystem.CanReachBeyondThisComputer(printerName)
			&& !this.IsListed(printerName))
		{
			return null;
		}

		WindowsPrinterRecord? printer = this.context.Spool.GetPrinter(printerName);

		if (printer is null)
		{
			return null;
		}

		// Printer names are case-insensitive on Windows
		bool isDefault = string.Equals(printer.Name, this.context.Spool.GetDefaultPrinterName(), StringComparison.OrdinalIgnoreCase);
		PrinterInfo info = WindowsPrinterInfoMapper.ToPrinterInfo(printer, WindowsPrintSystem.PrinterIdPrefix + printer.Name, isDefault);

		return new WindowsPrinter(this.context, printer.Name, printer.DriverName, printer.PortName, info);
	}
}
