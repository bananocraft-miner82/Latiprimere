using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp;

/// <summary>
/// Reports answers from a printer or print server that Latiprimere can't read.
/// </summary>
/// <remarks>
/// Mapping what a printer or server sent can fail on data no well-behaved one sends. Such a failure is the print
/// system's, not a programming error, so it becomes a <see cref="PrintException"/>, which the printer catalog
/// isolates (ADR 0008), rather than taking down a listing of every print system.
/// </remarks>
internal static class IppResponseErrors
{
	/// <summary>
	/// Checks whether an exception came from data that couldn't be read, rather than from a programming error.
	/// </summary>
	/// <param name="exception">The exception thrown while mapping a response.</param>
	/// <returns><see langword="true"/> for a format, overflow or type mismatch in the data.</returns>
	internal static bool IsUnreadableData(Exception exception)
	{
		return exception is FormatException or OverflowException or InvalidCastException;
	}

	/// <summary>
	/// Creates the exception for an answer that couldn't be read.
	/// </summary>
	/// <param name="source">The printer or server that answered.</param>
	/// <param name="innerException">What failed while reading it.</param>
	/// <returns>The exception.</returns>
	internal static PrintException CreateUnreadable(Uri source, Exception innerException)
	{
		return new PrintException(ExceptionMessages.FormatIppResponseUnreadable(source), innerException);
	}
}
