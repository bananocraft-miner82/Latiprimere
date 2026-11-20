using System.Globalization;
using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Reports a request cancelled by <see cref="IppStallGuardHandler"/>, because the printer stopped reading its body or
/// didn't answer once it was sent. <see cref="SharpIppClientAdapter"/> turns it into a <see cref="PrintException"/>.
/// </summary>
internal sealed class IppTransferStalledException : TimeoutException
{
	/// <summary>
	/// Initialises a new instance of the <see cref="IppTransferStalledException"/> class.
	/// </summary>
	/// <param name="isAwaitingResponse">Whether the whole body had been sent, so it was the answer that didn't
	/// come.</param>
	/// <param name="stallTimeout">The limit that ran out.</param>
	/// <param name="innerException">How the cancelled request failed.</param>
	public IppTransferStalledException(bool isAwaitingResponse, TimeSpan stallTimeout, Exception? innerException)
		: base(IppTransferStalledException.CreateMessage(isAwaitingResponse, stallTimeout), innerException)
	{
		this.IsAwaitingResponse = isAwaitingResponse;
		this.StallTimeout       = stallTimeout;
	}

	/// <summary>
	/// Gets a value indicating whether the whole body had been sent, so it was the answer that didn't come.
	/// </summary>
	public bool IsAwaitingResponse { get; }

	/// <summary>
	/// Gets the limit that ran out.
	/// </summary>
	public TimeSpan StallTimeout { get; }

	/// <summary>
	/// Writes a stall timeout as seconds for a message, to the millisecond and no more: <c>300</c>, or <c>0.25</c>.
	/// </summary>
	/// <param name="stallTimeout">The timeout.</param>
	/// <returns>The seconds, in the current culture.</returns>
	internal static string FormatSeconds(TimeSpan stallTimeout)
	{
		return stallTimeout.TotalSeconds.ToString("0.###", CultureInfo.CurrentCulture);
	}

	/// <summary>
	/// Creates the message.
	/// </summary>
	/// <param name="isAwaitingResponse">Whether it was the answer that didn't come.</param>
	/// <param name="stallTimeout">The limit that ran out.</param>
	/// <returns>The message.</returns>
	private static string CreateMessage(bool isAwaitingResponse, TimeSpan stallTimeout)
	{
		string seconds = IppTransferStalledException.FormatSeconds(stallTimeout);

		return isAwaitingResponse
			? ExceptionMessages.FormatIppTransferStalledAwaitingResponse(seconds)
			: ExceptionMessages.FormatIppTransferStalledSending(seconds);
	}
}
