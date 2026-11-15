using System.Globalization;
using Latiprimere.Cli.Resources;

namespace Latiprimere.Cli;

/// <summary>
/// Writes formatted messages to the console.
/// </summary>
internal static class ConsoleOutput
{
	/// <summary>
	/// Writes a formatted message to standard output.
	/// </summary>
	/// <param name="format">The composite format string.</param>
	/// <param name="arguments">The values to insert.</param>
	internal static void Write(string format, params object?[] arguments)
	{
		Console.WriteLine(string.Format(CultureInfo.CurrentCulture, format, arguments));
	}

	/// <summary>
	/// Writes a formatted message to standard error.
	/// </summary>
	/// <param name="format">The composite format string.</param>
	/// <param name="arguments">The values to insert.</param>
	internal static void WriteError(string format, params object?[] arguments)
	{
		Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, format, arguments));
	}

	/// <summary>
	/// Writes a print failure, including the print system's error code if it gave one.
	/// </summary>
	/// <param name="exception">The failure.</param>
	internal static void WritePrintException(PrintException exception)
	{
		ConsoleOutput.WriteError(CliMessages.PrintFailed, exception.Message);

		if (exception.NativeErrorCode is not null)
		{
			ConsoleOutput.WriteError(CliMessages.NativeErrorCode, exception.NativeErrorCode);
		}
	}

	/// <summary>
	/// Writes the usage text.
	/// </summary>
	/// <returns>The exit code for invalid arguments, so callers can return it directly.</returns>
	internal static int WriteUsage()
	{
		Console.Error.WriteLine(CliMessages.Usage);

		return ExitCodes.InvalidArguments;
	}
}
