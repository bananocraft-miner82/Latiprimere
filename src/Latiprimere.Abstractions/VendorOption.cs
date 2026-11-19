namespace Latiprimere;

/// <summary>
/// A setting a printer offers beyond the typed options in <see cref="PrintOptions"/>, such as print quality,
/// stapling or a driver's own settings, with the values it takes.
/// </summary>
/// <remarks>
/// <para>
/// Each one can be used as it is: set <see cref="Key"/> in <see cref="PrintOptions.VendorOptions"/> to one of the
/// <see cref="Choices"/>' values, or to a value the <see cref="Kind"/> allows. For example, an IPP printer may offer
/// <c>ipp:print-quality</c> with the choices <c>3</c> (Draft), <c>4</c> (Normal) and <c>5</c> (High).
/// </para>
/// <para>
/// Vendor options aren't portable: keys start with the print system that reads them (<c>ipp:</c> or
/// <c>windows:</c>), and their values often only mean something to the printer that listed them. Settings the typed
/// options cover, such as paper size and duplex, aren't listed.
/// </para>
/// <para>
/// Records compare lists by reference, as <see cref="PrinterCapabilities"/> does.
/// </para>
/// </remarks>
public sealed record VendorOption
{
	/// <summary>
	/// Gets the key to set in <see cref="PrintOptions.VendorOptions"/>, such as <c>ipp:print-quality</c> or
	/// <c>windows:feature:psk:JobStapleAllDocuments</c>.
	/// </summary>
	public required string Key { get; init; }

	/// <summary>
	/// Gets a name to show people, such as "Print quality". Comes from the printer driver where it has one.
	/// </summary>
	public required string DisplayName { get; init; }

	/// <summary>
	/// Gets what kind of value the option takes: one of the <see cref="Choices"/>, a whole number, or text.
	/// </summary>
	public VendorOptionKind Kind { get; init; } = VendorOptionKind.Choice;

	/// <summary>
	/// Gets the values to choose from, for a <see cref="VendorOptionKind.Choice"/> option; empty otherwise.
	/// </summary>
	public IReadOnlyList<VendorOptionChoice> Choices { get; init; } = Array.Empty<VendorOptionChoice>();

	/// <summary>
	/// Gets the value the printer uses when a job doesn't set this option, or <see langword="null"/> if not
	/// reported.
	/// </summary>
	public string? DefaultValue { get; init; }

	/// <summary>
	/// Gets the smallest value an <see cref="VendorOptionKind.Number"/> option takes, or <see langword="null"/> if
	/// not reported.
	/// </summary>
	public long? MinimumValue { get; init; }

	/// <summary>
	/// Gets the largest value an <see cref="VendorOptionKind.Number"/> option takes, or <see langword="null"/> if
	/// not reported.
	/// </summary>
	public long? MaximumValue { get; init; }

	/// <summary>
	/// Gets whether a job can set several of the <see cref="Choices"/> at once, separated by commas, as with IPP
	/// <c>finishings</c> such as <c>4,5</c> (staple and punch).
	/// </summary>
	public bool AllowsSeveralValues { get; init; }
}
