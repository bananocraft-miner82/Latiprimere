using System.Runtime.CompilerServices;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows;

/// <summary>
/// Follows a Windows printer or job: reads its status, then waits for the spooler to signal a change before reading
/// it again, or polls when the spooler can't signal (ADR 0049).
/// </summary>
internal static class WindowsStatusWatch
{
	/// <summary>
	/// The longest a status goes unread while waiting for a signal, in case a change isn't signalled: a driver's own
	/// status, such as paper out, may not be.
	/// </summary>
	internal static readonly TimeSpan SafetyInterval = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Follows a status.
	/// </summary>
	/// <typeparam name="T">The status type.</typeparam>
	/// <param name="openNotification">Opens the spooler's notification, or gives <see langword="null"/> when it
	/// can't; run off the caller's thread, as it blocks.</param>
	/// <param name="read">Reads the status.</param>
	/// <param name="hasChanged">Whether a status differs from the last one reported, or <see langword="null"/>.</param>
	/// <param name="isFinal">Whether a status is the last.</param>
	/// <param name="pollInterval">How long to wait between reads without a notification.</param>
	/// <param name="onFailure">Turns a failure to read the status into a status, so the watch goes on; or
	/// <see langword="null"/> to let the failure end it.</param>
	/// <param name="cancellationToken">A token to stop following.</param>
	/// <returns>Each distinct status, ending with the final one if there is one.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> is zero or negative.</exception>
	public static async IAsyncEnumerable<T> WatchAsync<T>
	(
		Func<IWindowsChangeNotification?>          openNotification,
		Func<CancellationToken, Task<T>>           read,
		Func<T?, T, bool>                          hasChanged,
		Func<T, bool>                              isFinal,
		TimeSpan                                   pollInterval,
		Func<PrintException, T>?                   onFailure,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
		where T : class
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

		// Opened before the first read, so a change between the read and the wait still signals
		IWindowsChangeNotification? notification = await Task.Run(openNotification, cancellationToken).ConfigureAwait(false);
		T? previous = null;

		try
		{
			while (true)
			{
				T current = await WindowsStatusWatch.ReadAsync(read, onFailure, cancellationToken).ConfigureAwait(false);

				if (hasChanged(previous, current))
				{
					yield return current;
				}

				if (isFinal(current))
				{
					yield break;
				}

				previous = current;

				if (notification is null)
				{
					await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					await notification.WaitAsync(WindowsStatusWatch.SafetyInterval, cancellationToken).ConfigureAwait(false);

					// A notification that can't be reset would signal for ever, so it gives way to polling
					if (notification.HasFailed)
					{
						notification.Dispose();
						notification = null;
					}
				}
			}
		}
		finally
		{
			notification?.Dispose();
		}
	}

	/// <summary>
	/// Reads a status, giving a failure to read it as a status when the caller says how.
	/// </summary>
	/// <typeparam name="T">The status type.</typeparam>
	/// <param name="read">Reads the status.</param>
	/// <param name="onFailure">Turns a failure into a status, or <see langword="null"/> to let it end the watch.</param>
	/// <param name="cancellationToken">A token to stop reading.</param>
	/// <returns>The status.</returns>
	private static async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, Func<PrintException, T>? onFailure, CancellationToken cancellationToken)
	{
		try
		{
			return await read(cancellationToken).ConfigureAwait(false);
		}
		catch (PrintException exception) when (onFailure is not null)
		{
			return onFailure(exception);
		}
	}

	/// <summary>
	/// Checks whether a job's status has changed in a way worth reporting.
	/// </summary>
	/// <param name="previous">The status last reported, or <see langword="null"/>.</param>
	/// <param name="current">The status just read.</param>
	/// <returns>Whether its state, message, reasons or page count differ.</returns>
	public static bool HasChanged(PrintJobStatus? previous, PrintJobStatus current)
	{
		return previous is null
			|| previous.State != current.State
			|| previous.StateMessage != current.StateMessage
			|| previous.PagesCompleted != current.PagesCompleted
			|| !previous.StateReasons.SequenceEqual(current.StateReasons, StringComparer.Ordinal);
	}

	/// <summary>
	/// Checks whether a printer's status has changed in a way worth reporting.
	/// </summary>
	/// <param name="previous">The status last reported, or <see langword="null"/>.</param>
	/// <param name="current">The status just read.</param>
	/// <returns>Whether its state, message, reasons or whether it accepts jobs differ.</returns>
	public static bool HasChanged(PrinterStatus? previous, PrinterStatus current)
	{
		return previous is null
			|| previous.State != current.State
			|| previous.StateMessage != current.StateMessage
			|| previous.IsAcceptingJobs != current.IsAcceptingJobs
			|| !previous.StateReasons.SequenceEqual(current.StateReasons, StringComparer.Ordinal);
	}
}
