namespace Latiprimere.Windows;

/// <summary>
/// The status a printer watch reports when the printer's state can't be read, so the watch goes on (ADR 0049).
/// </summary>
internal static class PrinterStatusFailure
{
	/// <summary>
	/// Creates the status.
	/// </summary>
	/// <param name="printerId">The printer's identifier.</param>
	/// <param name="exception">Why the state couldn't be read.</param>
	/// <param name="timestamp">When.</param>
	/// <returns>An unknown state, with the failure's message.</returns>
	public static PrinterStatus Create(string printerId, PrintException exception, DateTimeOffset timestamp)
	{
		ArgumentNullException.ThrowIfNull(exception);

		return new PrinterStatus
		{
			PrinterId    = printerId,
			State        = PrinterState.Unknown,
			StateMessage = exception.Message,
			Timestamp    = timestamp
		};
	}
}
