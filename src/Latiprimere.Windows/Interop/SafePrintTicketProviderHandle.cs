using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// A Print Ticket provider from <c>PTOpenProvider</c>, closed with <c>PTCloseProvider</c>.
/// </summary>
/// <remarks>
/// A provider belongs to the thread that opened it, so it is opened, used and closed in one call.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class SafePrintTicketProviderHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	/// <summary>
	/// Initialises a new instance of the <see cref="SafePrintTicketProviderHandle"/> class. Called by the interop
	/// marshaller.
	/// </summary>
	public SafePrintTicketProviderHandle()
		: base(ownsHandle: true)
	{
	}

	/// <summary>
	/// Closes the provider.
	/// </summary>
	/// <returns><see langword="true"/> if it was closed.</returns>
	protected override bool ReleaseHandle()
	{
		return PrintTicketNative.PTCloseProvider(this.handle) >= 0;
	}
}
