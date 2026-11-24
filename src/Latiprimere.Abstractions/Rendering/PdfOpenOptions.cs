using System.Text;

namespace Latiprimere.Rendering;

/// <summary>
/// Describes how a PDF is opened for rasterising.
/// </summary>
/// <remarks>
/// Properties may be added in a later version, each with a default that keeps how documents open today, so code that
/// passes these options keeps working.
/// </remarks>
public sealed record PdfOpenOptions
{
	/// <summary>
	/// The options used when a caller specifies none: no password.
	/// </summary>
	public static readonly PdfOpenOptions Default = new PdfOpenOptions();

	/// <summary>
	/// Gets the password that opens the document, or <see langword="null"/> for a document without one.
	/// </summary>
	/// <remarks>
	/// Either of a PDF's passwords opens it for rendering: the one for opening it, or the owner's. The password is
	/// never written to logs, and <see cref="ToString"/> shows only whether there is one.
	/// </remarks>
	public string? Password { get; init; }

	/// <summary>
	/// Writes the options for <see cref="ToString"/>, without the password itself.
	/// </summary>
	/// <param name="builder">The text being built.</param>
	/// <returns><see langword="true"/>, as there is always something to write.</returns>
	private bool PrintMembers(StringBuilder builder)
	{
		builder.Append("Password = ");
		builder.Append(this.Password is null ? "null" : "(set)");

		return true;
	}
}
