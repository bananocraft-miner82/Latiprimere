using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Maps a printer's IPP state attributes to a <see cref="PrinterStatus"/> (ADR 0049).
/// </summary>
internal static class IppPrinterStatusMapper
{
	/// <summary>The attributes a printer's status needs.</summary>
	internal static readonly string[] Attributes =
	[
		IppRequestedAttributes.PrinterState,
		IppRequestedAttributes.PrinterStateMessage,
		"printer-state-reasons",
		"printer-is-accepting-jobs",
	];

	/// <summary>
	/// Maps a printer's attributes to its status.
	/// </summary>
	/// <param name="attributes">The printer's attributes.</param>
	/// <param name="printerId">The printer's identifier.</param>
	/// <param name="timestamp">When they were read.</param>
	/// <returns>The status: its reasons without <c>none</c>, in the order given, each once.</returns>
	public static PrinterStatus ToPrinterStatus(PrinterDescriptionAttributes attributes, string printerId, DateTimeOffset timestamp)
	{
		ArgumentNullException.ThrowIfNull(attributes);

		string? message = IppValueReader.ReadString(attributes.PrinterStateMessage);

		return new PrinterStatus
		{
			PrinterId       = printerId,
			State           = IppPrinterInfoMapper.ToPrinterState(IppValueReader.ReadValue(attributes.PrinterState)),
			StateMessage    = string.IsNullOrWhiteSpace(message) ? null : message,
			StateReasons    = IppPrinterStatusMapper.ReadReasons(attributes),
			IsAcceptingJobs = IppValueReader.ReadValue(attributes.PrinterIsAcceptingJobs),
			Timestamp       = timestamp
		};
	}

	/// <summary>
	/// Reads <c>printer-state-reasons</c>.
	/// </summary>
	/// <param name="attributes">The printer's attributes.</param>
	/// <returns>The keywords, lower case, without <c>none</c>, each once.</returns>
	private static string[] ReadReasons(PrinterDescriptionAttributes attributes)
	{
		PrinterStateReason[]? reasons = IppValueReader.ReadReference(attributes.PrinterStateReasons);

		if (reasons is null)
		{
			return [];
		}

		return reasons
			.Select(reason => reason.Value?.Trim().ToLowerInvariant())
			.OfType<string>()
			.Where(keyword => keyword.Length > 0 && keyword != "none")
			.Distinct(StringComparer.Ordinal)
			.ToArray();
	}
}
