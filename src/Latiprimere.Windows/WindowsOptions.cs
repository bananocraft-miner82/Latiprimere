namespace Latiprimere.Windows;

/// <summary>
/// Configures printing through the Windows print spooler.
/// </summary>
public sealed class WindowsOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether a printer identifier may name any printer, including one on a print
	/// server the user hasn't connected to (<c>windows:\\server\printer</c>). Defaults to <see langword="false"/>:
	/// a name with a backslash or a comma is looked up only if Windows lists it, as a local printer or as a
	/// connection the user already made.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Looking up <c>\\server\printer</c> contacts that server, and Windows may sign in to it with the
	/// application's account, sending a response that can be relayed or cracked. A comma opens special spooler
	/// handles, such as <c>name,Job 5</c>. When identifiers can come from outside the application, such as from a
	/// web request, allowing any name would let others make it contact any host.
	/// </para>
	/// <para>
	/// Turn this on to look up printers on servers that aren't connected yet, when every identifier the application
	/// looks up is trusted. Plain local printer names work either way.
	/// </para>
	/// </remarks>
	public bool AllowAnyPrinterName { get; set; }
}
