namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Text has a character Latiprimere's layout can't show (ADR 0048). A <see cref="NotSupportedException"/> to callers;
/// its own type so a Windows queue's IPP route doesn't take it for a raster job the printer can't take, and try the
/// driver, which would refuse the same text.
/// </summary>
internal sealed class TextCharacterNotSupportedException : NotSupportedException
{
	/// <summary>
	/// Initialises a new instance of the <see cref="TextCharacterNotSupportedException"/> class.
	/// </summary>
	public TextCharacterNotSupportedException()
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="TextCharacterNotSupportedException"/> class with a message.
	/// </summary>
	/// <param name="message">The message, naming the character and where it is.</param>
	public TextCharacterNotSupportedException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="TextCharacterNotSupportedException"/> class with a message and
	/// the exception that caused it.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The cause.</param>
	public TextCharacterNotSupportedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
