using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// The exception thrown when a print system can't be used: no supported print system is available on the current
/// platform, usually because no Latiprimere platform adapter has been registered for it, or a print system's own
/// service isn't running, such as the Windows Print Spooler.
/// </summary>
/// <remarks>
/// The printer catalog treats a print system that throws this like any other failing print system: it skips it
/// while others answer, and throws only if every print system fails.
/// </remarks>
public sealed class PrintSystemUnavailableException : PrintException
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PrintSystemUnavailableException"/> class with a default message.
	/// </summary>
	public PrintSystemUnavailableException()
		: base(ExceptionMessages.PrintSystemUnavailable)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintSystemUnavailableException"/> class with the given message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public PrintSystemUnavailableException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintSystemUnavailableException"/> class with the given message
	/// and cause.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public PrintSystemUnavailableException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
