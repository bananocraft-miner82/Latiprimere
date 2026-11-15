using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// The exception thrown when a print system needs a user name and password for a request, or doesn't accept the ones
/// it was given.
/// </summary>
/// <remarks>
/// Catch it to ask the user for credentials, then create the print system again with them; for CUPS, through
/// <c>CupsOptions.Credentials</c>. <see cref="PrintException.NativeErrorCode"/> holds the HTTP or IPP status the
/// print system answered with, such as 401.
/// </remarks>
public sealed class PrintAuthenticationException : PrintException
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PrintAuthenticationException"/> class with a default message.
	/// </summary>
	public PrintAuthenticationException()
		: base(ExceptionMessages.AuthenticationRequired)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintAuthenticationException"/> class with the given message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public PrintAuthenticationException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintAuthenticationException"/> class with the given message and
	/// cause.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public PrintAuthenticationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
