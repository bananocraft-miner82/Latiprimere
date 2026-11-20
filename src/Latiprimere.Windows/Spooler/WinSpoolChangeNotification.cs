using System.Runtime.Versioning;
using Latiprimere.Windows.Interop;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// A spooler change notification (<c>FindFirstPrinterChangeNotification</c>), waited on by the thread pool, so no
/// thread is held while nothing changes (ADR 0049).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WinSpoolChangeNotification : IWindowsChangeNotification
{
	private const uint PrinterChangePrinter = 0x000000FF; // PRINTER_CHANGE_PRINTER: settings and status
	private const uint PrinterChangeJob = 0x0000FF00;     // PRINTER_CHANGE_JOB: jobs added, set, written, deleted

	private readonly SafePrinterHandle printer;  // The printer, open while the notification is
	private readonly IntPtr change;              // The notification's handle
	private readonly NativeWaitHandle waitHandle; // The same handle, for the thread pool
	private int disposed;                        // 1 once disposed
	private volatile bool hasFailed;             // Whether a change couldn't be collected

	/// <summary>
	/// Initialises a new instance of the <see cref="WinSpoolChangeNotification"/> class.
	/// </summary>
	/// <param name="printer">The printer's handle, now owned by this.</param>
	/// <param name="change">The notification's handle, now owned by this.</param>
	private WinSpoolChangeNotification(SafePrinterHandle printer, IntPtr change)
	{
		this.printer = printer;
		this.change = change;
		this.waitHandle = new NativeWaitHandle(change);
	}

	/// <summary>
	/// Opens a change notification for a printer.
	/// </summary>
	/// <param name="printerName">The printer.</param>
	/// <param name="changes">What to be signalled for.</param>
	/// <returns>The notification, or <see langword="null"/> if the printer is gone or the spooler refused it, as
	/// some print servers' queues do: then the caller polls.</returns>
	public static WinSpoolChangeNotification? TryOpen(string printerName, WindowsChanges changes)
	{
		SafePrinterHandle? printer;

		try
		{
			printer = PrinterHandles.TryOpen(printerName);
		}
		catch (PrintException)
		{
			return null;
		}

		if (printer is null)
		{
			return null;
		}

		uint filter = changes == WindowsChanges.Jobs ? WinSpoolChangeNotification.PrinterChangeJob : WinSpoolChangeNotification.PrinterChangePrinter;
		IntPtr change = WinSpoolNative.FindFirstPrinterChangeNotification(printer, filter, 0, IntPtr.Zero);

		if (change == IntPtr.Zero || change == new IntPtr(-1))
		{
			printer.Dispose();

			return null;
		}

		return new WinSpoolChangeNotification(printer, change);
	}

	/// <inheritdoc/>
	public bool HasFailed
	{
		get
		{
			return this.hasFailed;
		}
	}

	/// <inheritdoc/>
	public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) == 1, this);

		TaskCompletionSource<bool> signalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		RegisteredWaitHandle registration = ThreadPool.RegisterWaitForSingleObject
		(
			this.waitHandle,
			static (state, timedOut) => ((TaskCompletionSource<bool>)state!).TrySetResult(!timedOut),
			signalled,
			timeout,
			executeOnlyOnce: true
		);

		try
		{
			using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), signalled))
			{
				if (!await signalled.Task.ConfigureAwait(false))
				{
					return false;
				}
			}

			// The handle stays signalled until the change is collected; if it can't be, as when a print server's
			// connection is lost, every later wait would return at once, so the caller is told to poll instead
			if (!WinSpoolNative.FindNextPrinterChangeNotification(this.change, out _, IntPtr.Zero, IntPtr.Zero))
			{
				this.hasFailed = true;
			}

			return true;
		}
		finally
		{
			registration.Unregister(null);
		}
	}

	/// <summary>
	/// Closes the notification, then the printer handle.
	/// </summary>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref this.disposed, 1) == 1)
		{
			return;
		}

		this.waitHandle.Dispose();
		WinSpoolNative.FindClosePrinterChangeNotification(this.change);
		this.printer.Dispose();
	}
}
