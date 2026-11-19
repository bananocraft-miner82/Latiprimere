namespace Latiprimere.Windows;

/// <summary>
/// A printer managed by the Windows spooler, with what only Windows offers: its driver's Print Capabilities and
/// Print Tickets.
/// </summary>
/// <remarks>
/// <para>
/// Every <c>windows:</c> printer the catalog returns implements this interface, so test for it:
/// </para>
/// <code>
/// if (printer is IWindowsPrinter windowsPrinter)
/// {
///     string capabilities = await windowsPrinter.GetPrintCapabilitiesAsync();
/// }
/// </code>
/// <para>
/// Print Capabilities and Print Tickets are XML documents in Microsoft's Print Schema. A Print Ticket chooses
/// settings; it can be passed to a job as the vendor option <c>windows:print-ticket</c>. Most applications don't
/// need either: <see cref="PrinterCapabilities.VendorOptions"/> lists the same features as vendor options, ready to
/// set.
/// </para>
/// <para>
/// A driver without Print Ticket support, such as an old GDI driver whose provider can't be opened, makes these
/// methods throw <see cref="PrintException"/>.
/// </para>
/// </remarks>
public interface IWindowsPrinter : IPrinter
{
	/// <summary>
	/// Gets the spooler's name for the printer's queue, as Windows shows it.
	/// </summary>
	string QueueName { get; }

	/// <summary>
	/// Reads the driver's Print Capabilities: every feature and parameter it offers, with their options and
	/// limits, the driver's own among them.
	/// </summary>
	/// <param name="printTicket">
	/// A Print Ticket whose choices constrain the result, merged onto the driver's defaults, such as one choosing a
	/// tray, so only the media types that tray takes are offered; or <see langword="null"/> for the defaults alone.
	/// </param>
	/// <param name="cancellationToken">A token to cancel waiting for the result.</param>
	/// <returns>The Print Capabilities document, as XML.</returns>
	/// <exception cref="ArgumentException">The Print Ticket isn't XML, isn't a Print Ticket, or the driver finds it
	/// malformed.</exception>
	/// <exception cref="PrintException">The driver's Print Ticket support failed, or the printer is gone.</exception>
	Task<string> GetPrintCapabilitiesAsync(string? printTicket = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads the driver's default settings as a full Print Ticket: a starting point for writing tickets, and a
	/// record of what a job gets when it chooses nothing.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel waiting for the result.</param>
	/// <returns>The Print Ticket, as XML.</returns>
	/// <exception cref="PrintException">The driver's Print Ticket support failed, or the printer is gone.</exception>
	Task<string> GetDefaultPrintTicketAsync(CancellationToken cancellationToken = default);
}
