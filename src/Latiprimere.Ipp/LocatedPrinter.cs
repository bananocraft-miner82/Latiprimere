using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp;

/// <summary>
/// A printer's address and the attributes read there.
/// </summary>
internal sealed record LocatedPrinter
{
	/// <summary>Gets the URI the attributes were read from.</summary>
	public required Uri PrinterUri { get; init; }

	/// <summary>Gets the printer's attributes.</summary>
	public required PrinterDescriptionAttributes Attributes { get; init; }
}
