using Latiprimere.Cli.Resources;

namespace Latiprimere.Cli.Commands;

/// <summary>
/// The <c>caps</c> command: shows what a printer can do.
/// </summary>
internal static class CapabilitiesCommand
{
	/// <summary>The command name.</summary>
	internal const string Name = "caps";

	/// <summary>
	/// Reads and shows a printer's capabilities, marking the defaults.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="arguments">The arguments after the command name: the printer ID.</param>
	/// <returns>The process exit code.</returns>
	internal static async Task<int> RunAsync(IPrinterCatalog catalog, string[] arguments)
	{
		if (arguments.Length != 1)
		{
			ConsoleOutput.WriteError(CliMessages.InvalidArguments, CliMessages.MissingCapabilitiesArguments);

			return ExitCodes.InvalidArguments;
		}

		IPrinter printer = await catalog.GetPrinterAsync(arguments[0], CancellationToken.None);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(CancellationToken.None);

		ConsoleOutput.Write(CliMessages.CapabilitiesHeading, printer.Info.Name, printer.Info.Id);

		CapabilitiesCommand.WriteLine
		(
			CliMessages.CapabilityPaperSizes,
			capabilities.MediaSizes.Select(size => CapabilitiesCommand.Mark
			(
				CliMessages.FormatPaperSizeValue(size.DisplayName, size.Name),
				size.Name == capabilities.DefaultMediaSize?.Name
			))
		);

		CapabilitiesCommand.WriteLine
		(
			CliMessages.CapabilityDuplex,
			capabilities.DuplexModes.Select
			(
				mode => CapabilitiesCommand.Mark
				(
					mode.ToString(),
					mode == capabilities.DefaultDuplexMode
				)
			)
		);

		CapabilitiesCommand.WriteLine
		(
			CliMessages.CapabilityColour,
			capabilities.ColorModes.Select
			(
				mode => CapabilitiesCommand.Mark
				(
					mode.ToString(),
					mode == capabilities.DefaultColorMode
				)
			)
		);

		CapabilitiesCommand.WriteLine
		(
			CliMessages.CapabilityCopies,
			capabilities.MaxCopies is int maxCopies
				? new[] { CliMessages.FormatMaxCopiesValue(maxCopies) }
				: Array.Empty<string>()
		);

		CapabilitiesCommand.WriteLine
		(
			CliMessages.CapabilityOrientations,
			capabilities.Orientations.Select(orientation => orientation.ToString())
		);

		CapabilitiesCommand.WriteLine
		(
			CliMessages.CapabilityTrays,
			capabilities.InputTrays.Select(tray => CliMessages.FormatTrayValue(tray.Name, tray.Id))
		);

		CapabilitiesCommand.WriteLine
		(
			CliMessages.CapabilityFormats,
			capabilities.DocumentFormats.Select(format => format.ToString())
		);

		CapabilitiesCommand.WriteVendorOptions(capabilities.VendorOptions);

		return ExitCodes.Success;
	}

	/// <summary>
	/// Writes the vendor options, one per line, with the values each takes; the default is marked.
	/// </summary>
	/// <param name="options">The vendor options.</param>
	private static void WriteVendorOptions(IReadOnlyList<VendorOption> options)
	{
		if (options.Count == 0)
		{
			ConsoleOutput.Write(CliMessages.CapabilityLine, CliMessages.CapabilityVendorOptions, CliMessages.NotReported);

			return;
		}

		ConsoleOutput.Write(CliMessages.CapabilityLine, CliMessages.CapabilityVendorOptions, string.Empty);

		foreach (VendorOption option in options)
		{
			string values = option.Kind switch
			{
				VendorOptionKind.Choice => string.Join
				(
					", ",
					option.Choices.Select(choice => CapabilitiesCommand.Mark
					(
						CliMessages.FormatVendorChoiceValue(choice.Value, choice.DisplayName),
						choice.Value == option.DefaultValue
					))
				) + (option.AllowsSeveralValues ? CliMessages.VendorSeveralValues : string.Empty),
				VendorOptionKind.Number => CliMessages.FormatVendorNumberValue
				(
					option.MinValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "…",
					option.MaxValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "…"
				),
				_ => CliMessages.VendorTextValue,
			};

			if (option.Kind != VendorOptionKind.Choice && option.DefaultValue is not null)
			{
				values += CliMessages.DefaultMarker + " " + option.DefaultValue;
			}

			ConsoleOutput.Write(CliMessages.VendorOptionLine, option.Key, option.DisplayName, values);
		}
	}

	/// <summary>
	/// Writes one capability with its values, or says it isn't reported.
	/// </summary>
	/// <param name="label">The capability's label.</param>
	/// <param name="values">The values to list.</param>
	private static void WriteLine(string label, IEnumerable<string> values)
	{
		string text = string.Join(", ", values);

		ConsoleOutput.Write(CliMessages.CapabilityLine, label, text.Length > 0 ? text : CliMessages.NotReported);
	}

	/// <summary>
	/// Adds the default marker to a value when it is the default.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="isDefault">Whether it is the default.</param>
	/// <returns>The value, marked if it is the default.</returns>
	private static string Mark(string value, bool isDefault)
	{
		return isDefault ? value + CliMessages.DefaultMarker : value;
	}
}
