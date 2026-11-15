namespace Latiprimere.Cups;

/// <summary>
/// Enumerates the operating systems the CUPS adapter distinguishes between.
/// </summary>
internal enum CupsPlatform
{
	/// <summary>
	/// An operating system without a local CUPS scheduler, such as Windows.
	/// </summary>
	Other = 0,

	/// <summary>
	/// Linux, where CUPS is the standard print system.
	/// </summary>
	Linux = 1,

	/// <summary>
	/// macOS, which uses CUPS as its print system.
	/// </summary>
	MacOS = 2,
}
