using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// The exception thrown when a printing operation fails. Base class for all Latiprimere exceptions.
/// </summary>
public class PrintException : Exception
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PrintException"/> class with a default message.
	/// </summary>
	public PrintException()
		: base(ExceptionMessages.PrintFailed)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintException"/> class with the given message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public PrintException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintException"/> class with the given message and cause.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public PrintException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the error code reported by the native print system, such as a Win32 error, an IPP status code or an HTTP
	/// status code, or <see langword="null"/> if none applies.
	/// </summary>
	public int? NativeErrorCode { get; init; }
}
