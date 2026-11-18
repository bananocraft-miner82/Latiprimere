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

	/// <summary>Option: paper size, by PWG name.</summary>
	private const string PaperOption = "--paper";

	/// <summary>Option: colour mode.</summary>
	private const string ColorOption = "--color";

	/// <summary>Option: paper tray, by ID.</summary>
	private const string TrayOption = "--tray";

	/// <summary>Option: a vendor option, KEY=VALUE; may be repeated.</summary>
	private const string VendorOption = "--option";

	/// <summary>Option: a Print Ticket file, sent as the vendor option <c>windows:print-ticket</c>.</summary>
	private const string TicketOption = "--ticket";

	/// <summary>The vendor option a Print Ticket file is sent as.</summary>
	private const string PrintTicketKey = "windows:print-ticket";

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

	/// <summary>Gets the PWG name of the paper size to look up in the printer's capabilities, or <see langword="null"/>.</summary>
	public string? PaperName { get; init; }

	/// <summary>Gets the ID of the tray to look up in the printer's capabilities, or <see langword="null"/>.</summary>
	public string? TrayId { get; init; }

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
			PrintArguments.ColorOption     => PrintArguments.ApplyColor(parsed, value, index),
			PrintArguments.VendorOption    => PrintArguments.ApplyVendorOption(parsed, value, index),
			PrintArguments.TicketOption    => PrintArguments.ApplyTicket(parsed, value, index),
			PrintArguments.PaperOption when !string.IsNullOrWhiteSpace(value) => (parsed with { PaperName = value }, index + 2, null),
			PrintArguments.TrayOption when !string.IsNullOrWhiteSpace(value)  => (parsed with { TrayId = value }, index + 2, null),
			PrintArguments.PaperOption or PrintArguments.TrayOption =>
			(
				null,
				index,
				PrintArguments.Format(CliMessages.InvalidOptionValue, value, option)
			),
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
	/// Applies the <c>--color</c> option.
	/// </summary>
	/// <param name="parsed">The arguments parsed so far.</param>
	/// <param name="value">The option value: color or mono.</param>
	/// <param name="index">The position of the option.</param>
	/// <returns>The updated arguments (or <see langword="null"/> on error), the next position, and any error.</returns>
	private static (PrintArguments? Parsed, int NextIndex, string? Error) ApplyColor(PrintArguments parsed, string? value, int index)
	{
		ColorMode? colorMode = value switch
		{
			"color" => ColorMode.Color,
			"mono"  => ColorMode.Monochrome,
			_       => null,
		};

		if (colorMode is null)
		{
			return (null, index, PrintArguments.Format(CliMessages.InvalidOptionValue, value, PrintArguments.ColorOption));
		}

		return (parsed with { Options = parsed.Options with { ColorMode = colorMode.Value } }, index + 2, null);
	}

	/// <summary>
	/// Applies an <c>--option KEY=VALUE</c> option, adding to any given before.
	/// </summary>
	/// <param name="parsed">The arguments parsed so far.</param>
	/// <param name="value">The option value, such as <c>ipp:print-quality=5</c>.</param>
	/// <param name="index">The position of the option.</param>
	/// <returns>The updated arguments (or <see langword="null"/> on error), the next position, and any error.</returns>
	private static (PrintArguments? Parsed, int NextIndex, string? Error) ApplyVendorOption(PrintArguments parsed, string? value, int index)
	{
		int separator = value?.IndexOf('=', StringComparison.Ordinal) ?? -1;

		if (value is null || separator <= 0)
		{
			return (null, index, PrintArguments.Format(CliMessages.InvalidOptionValue, value, PrintArguments.VendorOption));
		}

		Dictionary<string, string> vendorOptions = new Dictionary<string, string>(parsed.Options.VendorOptions, StringComparer.Ordinal)
		{
			[value[..separator]] = value[(separator + 1)..]
		};

		try
		{
			return (parsed with { Options = parsed.Options with { VendorOptions = vendorOptions } }, index + 2, null);
		}
		catch (ArgumentException exception)
		{
			// The key has no ipp:, cups: or windows: prefix
			return (null, index, exception.Message);
		}
	}

	/// <summary>
	/// Applies a <c>--ticket FILE</c> option: the file's text becomes the vendor option <c>windows:print-ticket</c>.
	/// </summary>
	/// <param name="parsed">The arguments parsed so far.</param>
	/// <param name="value">The file's path.</param>
	/// <param name="index">The position of the option.</param>
	/// <returns>The updated arguments (or <see langword="null"/> on error), the next position, and any error.</returns>
	private static (PrintArguments? Parsed, int NextIndex, string? Error) ApplyTicket(PrintArguments parsed, string? value, int index)
	{
		if (string.IsNullOrWhiteSpace(value) || !File.Exists(value))
		{
			return (null, index, PrintArguments.Format(CliMessages.InvalidOptionValue, value, PrintArguments.TicketOption));
		}

		Dictionary<string, string> vendorOptions = new Dictionary<string, string>(parsed.Options.VendorOptions, StringComparer.Ordinal)
		{
			[PrintArguments.PrintTicketKey] = File.ReadAllText(value)
		};

		return (parsed with { Options = parsed.Options with { VendorOptions = vendorOptions } }, index + 2, null);
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
