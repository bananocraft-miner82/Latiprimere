namespace Latiprimere.ApiReference;

/// <summary>
/// Where a reference to a type or member links to, and what it shows.
/// </summary>
internal sealed record LinkTarget
{
	/// <summary>Gets the page's URL, with the member's anchor for a member.</summary>
	public required string Url { get; init; }

	/// <summary>Gets the text to show: the type's name, or <c>Type.Member</c>.</summary>
	public required string Text { get; init; }
}
