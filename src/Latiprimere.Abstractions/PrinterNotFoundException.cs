using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// The exception thrown when a requested printer does not exist.
/// </summary>
public sealed class PrinterNotFoundException : PrintException
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PrinterNotFoundException"/> class with a default message.
	/// </summary>
	public PrinterNotFoundException()
		: base(ExceptionMessages.PrinterNotFound)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrinterNotFoundException"/> class with the given message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public PrinterNotFoundException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrinterNotFoundException"/> class with the given message and cause.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public PrinterNotFoundException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the identifier of the printer that was not found, if known.
	/// </summary>
	public string? PrinterId { get; init; }

	/// <summary>
	/// Creates an exception for a printer identifier that was not found, with a standard message.
	/// </summary>
	/// <param name="printerId">The identifier of the printer that was not found.</param>
	/// <returns>The exception, ready to throw.</returns>
	public static PrinterNotFoundException FromPrinterId(string printerId)
	{
		// Build the localised message and record the identifier for callers to inspect
		string message = ExceptionMessages.FormatPrinterNotFoundWithId(printerId);

		return new PrinterNotFoundException(message)
		{
			PrinterId = printerId
		};
	}
}
