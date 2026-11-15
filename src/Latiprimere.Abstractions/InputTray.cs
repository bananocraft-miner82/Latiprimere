namespace Latiprimere;

/// <summary>
/// A paper source on a printer, such as a tray or the manual feed.
/// </summary>
public sealed record InputTray
{
	/// <summary>
	/// Gets the print system's identifier for the tray: an IPP <c>media-source</c> keyword such as
	/// <c>by-pass-tray</c>, or a Windows bin number. Only meaningful to the printer that reported it.
	/// </summary>
	public required string Id { get; init; }

	/// <summary>
	/// Gets a name to show people, such as "Manual feed". Comes from the printer driver where it has one.
	/// </summary>
	public required string Name { get; init; }
}
