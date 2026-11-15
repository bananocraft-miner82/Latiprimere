using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// A printer device context from <c>CreateDC</c>, deleted with <c>DeleteDC</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class SafeDeviceContextHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	/// <summary>
	/// Initialises a new instance of the <see cref="SafeDeviceContextHandle"/> class. Called by the interop marshaller.
	/// </summary>
	public SafeDeviceContextHandle()
		: base(ownsHandle: true)
	{
	}

	/// <summary>
	/// Deletes the device context.
	/// </summary>
	/// <returns><see langword="true"/> if the device context was deleted.</returns>
	protected override bool ReleaseHandle()
	{
		return GdiNative.DeleteDC(this.handle);
	}
}
