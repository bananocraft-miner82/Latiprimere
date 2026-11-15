namespace Latiprimere;

/// <summary>
/// Describes a printer known to a print system.
/// </summary>
public sealed record PrinterInfo
{
	/// <summary>
	/// Gets the identifier used to look up the printer. Unique within the printer catalog.
	/// </summary>
	/// <remarks>
	/// Treat it as opaque: store it and pass it to <see cref="IPrinterCatalog.GetPrinterAsync"/> later, even after a
	/// restart, but don't parse it. It stays the same for as long as the printer keeps its name in its print system.
	/// A network printer found by discovery is identified by its announced name, so renaming it changes its
	/// identifier; configure it by URI where an identifier must never change.
	/// </remarks>
	public required string Id { get; init; }

	/// <summary>
	/// Gets the printer's display name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the printer's description, if the print system provides one.
	/// </summary>
	public string? Description { get; init; }

	/// <summary>
	/// Gets the printer's location, if the print system provides one.
	/// </summary>
	public string? Location { get; init; }

	/// <summary>
	/// Gets the printer's make and model, if the print system provides it.
	/// </summary>
	public string? MakeAndModel { get; init; }

	/// <summary>
	/// Gets the printer's state when this information was read.
	/// </summary>
	public PrinterState State { get; init; } = PrinterState.Unknown;

	/// <summary>
	/// Gets a value indicating whether this is the default printer of its print system.
	/// </summary>
	public bool IsDefault { get; init; }
}
