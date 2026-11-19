namespace Latiprimere;

/// <summary>
/// What kind of value a <see cref="VendorOption"/> takes.
/// </summary>
public enum VendorOptionKind
{
	/// <summary>One of the option's <see cref="VendorOption.Choices"/>, or several when it allows that.</summary>
	Choice = 0,

	/// <summary>A whole number, between <see cref="VendorOption.MinValue"/> and
	/// <see cref="VendorOption.MaxValue"/> where they are given.</summary>
	Number = 1,

	/// <summary>Text, which the printer's driver interprets.</summary>
	Text = 2,
}
