namespace Latiprimere;

/// <summary>
/// One of the values a <see cref="VendorOption"/> can be set to.
/// </summary>
public sealed record VendorOptionChoice
{
	/// <summary>
	/// Gets the value to set in <see cref="PrintOptions.VendorOptions"/>, such as <c>5</c> for IPP
	/// <c>print-quality</c>, or <c>psk:StapleTopLeft</c> for a Windows feature.
	/// </summary>
	public required string Value { get; init; }

	/// <summary>
	/// Gets a name to show people, such as "High" or "Staple top left". Comes from the printer driver where it has
	/// one.
	/// </summary>
	public required string DisplayName { get; init; }
}
