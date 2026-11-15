using Latiprimere.Cli.Resources;

namespace Latiprimere.Cli.Commands;

/// <summary>
/// The <c>list</c> command: lists the available printers.
/// </summary>
internal static class ListCommand
{
	/// <summary>The command name.</summary>
	internal const string Name = "list";

	/// <summary>
	/// Lists the available printers with their details.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <returns>The process exit code.</returns>
	internal static async Task<int> RunAsync(IPrinterCatalog catalog)
	{
		IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync(CancellationToken.None);

		// Say so plainly when there is nothing to list
		if (printers.Count == 0)
		{
			Console.WriteLine(CliMessages.NoPrinters);

			return ExitCodes.Success;
		}

		// One entry per printer, marking the default and showing whatever details the print system gave
		Console.WriteLine(CliMessages.PrintersHeading);

		foreach (PrinterInfo printer in printers)
		{
			string defaultMarker = printer.IsDefault ? CliMessages.DefaultMarker : string.Empty;

			ConsoleOutput.Write(CliMessages.PrinterLine, printer.Name, defaultMarker, printer.Id, printer.State);

			ListCommand.WriteDetails(printer.Description, printer.Location, printer.MakeAndModel);
		}

		return ExitCodes.Success;
	}

	/// <summary>
	/// Writes each detail that has a value on its own indented line.
	/// </summary>
	/// <param name="details">The details to write.</param>
	private static void WriteDetails(params string?[] details)
	{
		foreach (string? detail in details)
		{
			if (!string.IsNullOrWhiteSpace(detail))
			{
				ConsoleOutput.Write(CliMessages.PrinterDetailLine, detail);
			}
		}
	}
}
