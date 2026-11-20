using Microsoft.Win32.SafeHandles;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// A <see cref="WaitHandle"/> over a native handle it doesn't own, such as a printer change notification, so the
/// thread pool can wait on it.
/// </summary>
internal sealed class NativeWaitHandle : WaitHandle
{
	/// <summary>
	/// Initialises a new instance of the <see cref="NativeWaitHandle"/> class.
	/// </summary>
	/// <param name="handle">The native handle. Its owner closes it, after this.</param>
	public NativeWaitHandle(IntPtr handle)
	{
		this.SafeWaitHandle = new SafeWaitHandle(handle, ownsHandle: false);
	}
}
