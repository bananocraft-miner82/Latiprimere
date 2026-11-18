namespace Latiprimere.Windows.Spooler;

/// <summary>
/// The <c>windows:</c> vendor options of a job, read and checked (ADR 0026, ADR 0031).
/// </summary>
internal sealed record WindowsVendorSettings
{
	/// <summary>Gets settings with no <c>windows:</c> keys.</summary>
	internal static WindowsVendorSettings None { get; } = new WindowsVendorSettings();

	/// <summary>Gets a DEVMODE saved from the driver (<c>windows:devmode</c>), with its private data, to start from
	/// instead of the driver's defaults; or <see langword="null"/>.</summary>
	internal byte[]? DevMode { get; init; }

	/// <summary>Gets a Print Ticket (<c>windows:print-ticket</c>) to merge onto the starting DEVMODE, as UTF-8 XML;
	/// or <see langword="null"/>.</summary>
	internal byte[]? PrintTicket { get; init; }

	/// <summary>Gets the <c>windows:feature:</c> and <c>windows:parameter:</c> keys with their values, checked
	/// only for their form; empty if there are none.</summary>
	internal IReadOnlyDictionary<string, string> Features { get; init; } = new Dictionary<string, string>();
}
