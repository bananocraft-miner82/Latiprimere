namespace Latiprimere.Ipp;

/// <summary>
/// Tells failures to reach a printer apart from failures once connected.
/// </summary>
internal static class IppConnectionErrors
{
	/// <summary>
	/// Checks whether a failure happened before a connection to the printer opened, so nothing reached it.
	/// </summary>
	/// <param name="exception">The failure, as <see cref="SharpIppClientAdapter"/> reports it.</param>
	/// <returns>
	/// Whether the name didn't resolve, the connection was refused or failed to open, or the connect timeout ran out.
	/// A connection lost during an upload, a TLS failure or a rejection by the printer returns <see langword="false"/>.
	/// </returns>
	internal static bool IsConnectFailure(PrintException exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		return exception.InnerException switch
		{
			HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError } => true,

			// SocketsHttpHandler's connect timeout; Print-Job has no other timeout that ends this way
			OperationCanceledException { InnerException: TimeoutException } => true,

			_ => false,
		};
	}
}
