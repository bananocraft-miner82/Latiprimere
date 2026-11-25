namespace Latiprimere.Ipp.Documents;

/// <summary>
/// What a test page says about the printer it is printed on (ADR 0059).
/// </summary>
internal sealed record TestPageFacts
{
	/// <summary>Gets the printer's information.</summary>
	public required PrinterInfo Printer { get; init; }

	/// <summary>Gets the name of the printer's print system, such as "CUPS".</summary>
	public required string PrintSystem { get; init; }

	/// <summary>Gets where the print system reaches the printer: its URI, or a Windows queue's driver and port.</summary>
	public required string Address { get; init; }

	/// <summary>Gets what the printer's capabilities say.</summary>
	public required PrinterCapabilities Capabilities { get; init; }

	/// <summary>Gets the paper the page is laid out on.</summary>
	public required MediaSize Paper { get; init; }

	/// <summary>Gets when the page was made.</summary>
	public required DateTimeOffset PrintedAt { get; init; }
}
