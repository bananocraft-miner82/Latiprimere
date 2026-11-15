using System.Globalization;
using Latiprimere.Cli.Resources;

namespace Latiprimere.Cli.Commands;

/// <summary>
/// The parsed arguments of the <c>print</c> command.
/// </summary>
internal sealed record PrintArguments
{
	/// <summary>Option: send the file as raw printer data.</summary>
	private const string RawOption = "--raw";

	/// <summary>Option: number of copies.</summary>
	private const string CopiesOption = "--copies";

	/// <summary>Option: duplex mode.</summary>
	private const string DuplexOption = "--duplex";

	/// <summary>Option: landscape orientation.</summary>
	private const string LandscapeOption = "--landscape";

	/// <summary>Option: cancel straight after submitting.</summary>
	private const string CancelOption = "--cancel";

	/// <summary>Option: print to a file instead of the device.</summary>
	private const string OutputOption = "--output";

	/// <summary>Option: report timing and memory use.</summary>
	private const string StatsOption = "--stats";

	/// <summary>Gets the ID of the printer to print to.</summary>
	public required string PrinterId { get; init; }

	/// <summary>Gets the path of the file to print.</summary>
	public required string FilePath { get; init; }

	/// <summary>Gets a value indicating whether to send the file as raw printer data.</summary>
	public bool IsRaw { get; init; }

	/// <summary>Gets a value indicating whether to cancel the job straight after submitting it.</summary>
	public bool CancelAfterSubmit { get; init; }

	/// <summary>Gets a value indicating whether to report timing and memory use.</summary>
	public bool ShowStatistics { get; init; }

	/// <summary>Gets the print options.</summary>
	public PrintOptions Options { get; init; } = PrintOptions.Default;

	/// <summary>
	/// Parses the arguments that follow <c>print</c>.
	/// </summary>
	/// <param name="arguments">The arguments.</param>
	/// <param name="error">Describes the problem when parsing fails.</param>
	/// <returns>The parsed arguments, or <see langword="null"/> if they are invalid.</returns>
	internal static PrintArguments? Parse(string[] arguments, out string? error)
	{
		error = null;

		if (arguments.Length < 2)
		{
			error = CliMessages.MissingPrintArguments;

			return null;
		}

		PrintArguments? parsed = new PrintArguments { PrinterId = arguments[0], FilePath = arguments[1] };
		int index = 2;

		while (parsed is not null && index < arguments.Length)
		{
			(PrintArguments? next, int nextIndex, string? optionError) = PrintArguments.ApplyOption(parsed, arguments, index);

			parsed = next;
			index = nextIndex;
			error = optionError;
		}

		return parsed;
	}

	/// <summary>
	/// Applies the option at the given position.
	/// </summary>
	/// <param name="parsed">The arguments parsed so far.</param>
	/// <param name="arguments">All arguments.</param>
	/// <param name="index">The position of the option.</param>
	/// <returns>The updated arguments (or <see langword="null"/> on error), the next position, and any error.</returns>
	private static (PrintArguments? Parsed, int NextIndex, string? Error) ApplyOption(PrintArguments parsed, string[] arguments, int index)
	{
		string option = arguments[index];
		string? value = index + 1 < arguments.Length ? arguments[index + 1] : null;

		return option switch
		{
			PrintArguments.RawOption       => (parsed with { IsRaw = true }, index + 1, null),
			PrintArguments.CancelOption    => (parsed with { CancelAfterSubmit = true }, index + 1, null),
			PrintArguments.StatsOption     => (parsed with { ShowStatistics = true }, index + 1, null),
			PrintArguments.LandscapeOption =>
			(
				parsed with { Options = parsed.Options with { Orientation = PageOrientation.Landscape } },
				index + 1,
				null
			),
			PrintArguments.CopiesOption    => PrintArguments.ApplyCopies(parsed, value, index),
			PrintArguments.DuplexOption    => PrintArguments.ApplyDuplex(parsed, value, index),
			PrintArguments.OutputOption    => PrintArguments.ApplyOutput(parsed, value, index),
			_ => (null, index, PrintArguments.Format(CliMessages.UnknownOption, option)),
		};
	}

	/// <summary>
	/// Applies the <c>--output</c> option.
	/// </summary>
	/// <param name="parsed">The arguments parsed so far.</param>
	/// <param name="value">The output file path.</param>
	/// <param name="index">The position of the option.</param>
	/// <returns>The updated arguments (or <see langword="null"/> on error), the next position, and any error.</returns>
	private static (PrintArguments? Parsed, int NextIndex, string? Error) ApplyOutput(PrintArguments parsed, string? value, int index)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return (null, index, PrintArguments.Format(CliMessages.InvalidOptionValue, value, PrintArguments.OutputOption));
		}

		return (parsed with { Options = parsed.Options with { OutputFile = value } }, index + 2, null);
	}

	/// <summary>
	/// Applies the <c>--copies</c> option.
	/// </summary>
	/// <param name="parsed">The arguments parsed so far.</param>
	/// <param name="value">The option value.</param>
	/// <param name="index">The position of the option.</param>
	/// <returns>The updated arguments (or <see langword="null"/> on error), the next position, and any error.</returns>
	private static (PrintArguments? Parsed, int NextIndex, string? Error) ApplyCopies(PrintArguments parsed, string? value, int index)
	{
		bool isValid = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int copies)
					      && copies >= PrintOptions.MinimumCopies;

		if (!isValid)
		{
			return (null, index, PrintArguments.Format(CliMessages.InvalidOptionValue, value, PrintArguments.CopiesOption));
		}

		return (parsed with { Options = parsed.Options with { Copies = copies } }, index + 2, null);
	}

	/// <summary>
	/// Applies the <c>--duplex</c> option.
	/// </summary>
	/// <param name="parsed">The arguments parsed so far.</param>
	/// <param name="value">The option value: one, long or short.</param>
	/// <param name="index">The position of the option.</param>
	/// <returns>The updated arguments (or <see langword="null"/> on error), the next position, and any error.</returns>
	private static (PrintArguments? Parsed, int NextIndex, string? Error) ApplyDuplex(PrintArguments parsed, string? value, int index)
	{
		DuplexMode? duplex = value switch
		{
			"one"   => DuplexMode.OneSided,
			"long"  => DuplexMode.LongEdge,
			"short" => DuplexMode.ShortEdge,
			_       => null,
		};

		if (duplex is null)
		{
			return (null, index, PrintArguments.Format(CliMessages.InvalidOptionValue, value, PrintArguments.DuplexOption));
		}

		return (parsed with { Options = parsed.Options with { Duplex = duplex.Value } }, index + 2, null);
	}

	/// <summary>
	/// Formats a message with the current culture.
	/// </summary>
	/// <param name="format">The composite format string.</param>
	/// <param name="arguments">The values to insert.</param>
	/// <returns>The formatted message.</returns>
	private static string Format(string format, params object?[] arguments)
	{
		return string.Format(CultureInfo.CurrentCulture, format, arguments);
	}
}
