using System.Runtime.Versioning;

namespace Latiprimere.Windows.PrintTickets;

/// <summary>
/// Applies Print Tickets to a DEVMODE through the printer's driver (ADR 0031, ADR 0032).
/// </summary>
/// <remarks>
/// The DEVMODE becomes a full ticket, each ticket is merged onto it in turn and checked by the driver, and the result
/// becomes a DEVMODE again. Settings a ticket leaves out keep their values, and the driver's own features, in their
/// published namespaces, are reached as well as the Print Schema's.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class PrintTicketMerger
{
	/// <summary>
	/// Merges Print Tickets onto a DEVMODE, in order, so a later ticket wins over an earlier one.
	/// </summary>
	/// <param name="printerName">The printer whose driver reads them.</param>
	/// <param name="devMode">The DEVMODE to start from, with the driver's private data.</param>
	/// <param name="tickets">The Print Tickets, as UTF-8 XML.</param>
	/// <returns>The merged DEVMODE, with the driver's private data; the one given if there are no tickets.</returns>
	/// <exception cref="ArgumentException">The driver refused a ticket as malformed.</exception>
	/// <exception cref="PrintException">The Print Ticket API failed, or isn't available for this printer.</exception>
	internal static byte[] Merge(string printerName, byte[] devMode, IReadOnlyList<byte[]> tickets)
	{
		ArgumentNullException.ThrowIfNull(printerName);
		ArgumentNullException.ThrowIfNull(devMode);
		ArgumentNullException.ThrowIfNull(tickets);

		if (tickets.Count == 0)
		{
			return devMode;
		}

		using PrintTicketSession session = PrintTicketSession.Open(printerName);
		byte[] merged = session.ConvertDevModeToTicket(devMode);

		foreach (byte[] ticket in tickets)
		{
			merged = session.Merge(merged, ticket);
		}

		return session.ConvertTicketToDevMode(merged);
	}
}
