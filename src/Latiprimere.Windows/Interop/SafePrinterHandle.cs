using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// A printer handle from <c>OpenPrinter</c>, closed with <c>ClosePrinter</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class SafePrinterHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	/// <summary>
	/// Initialises a new instance of the <see cref="SafePrinterHandle"/> class. Called by the interop marshaller.
	/// </summary>
	public SafePrinterHandle()
		: base(ownsHandle: true)
	{
	}

	/// <summary>
	/// Closes the printer handle.
	/// </summary>
	/// <returns><see langword="true"/> if the handle was closed.</returns>
	protected override bool ReleaseHandle()
	{
		return WinSpoolNative.ClosePrinter(this.handle);
	}
}
