using Latiprimere.Cli.Resources;
using Latiprimere.Windows;

namespace Latiprimere.Cli.Commands;

/// <summary>
/// The <c>print-capabilities</c> command: writes a Windows printer driver's Print Capabilities, or its default Print
/// Ticket, as XML (ADR 0032). It shows every feature the driver offers, to write a <c>windows:print-ticket</c> from.
/// </summary>
internal static class PrintCapabilitiesCommand
{
	/// <summary>The command name.</summary>
	internal const string Name = "print-capabilities";

	/// <summary>Option: a Print Ticket file whose choices constrain the result.</summary>
	private const string TicketOption = "--ticket";

	/// <summary>Option: write the driver's default Print Ticket instead.</summary>
	private const string DefaultTicketOption = "--default-ticket";

	/// <summary>
	/// Writes the document to standard output.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="arguments">The arguments after the command name: the printer ID, then any options.</param>
	/// <returns>The process exit code.</returns>
	internal static async Task<int> RunAsync(IPrinterCatalog catalog, string[] arguments)
	{
		string? ticketFile = null;
		bool defaultTicket = false;

		for (int index = 1; index < arguments.Length; index++)
		{
			if (arguments[index] == PrintCapabilitiesCommand.TicketOption)
			{
				if (index + 1 >= arguments.Length)
				{
					string message = CliMessages.FormatInvalidOptionValue(string.Empty, PrintCapabilitiesCommand.TicketOption);
					ConsoleOutput.WriteError(CliMessages.InvalidArguments, message);

					return ExitCodes.InvalidArguments;
				}

				ticketFile = arguments[++index];
			}
			else if (arguments[index] == PrintCapabilitiesCommand.DefaultTicketOption)
			{
				defaultTicket = true;
			}
			else
			{
				ConsoleOutput.WriteError(CliMessages.InvalidArguments, CliMessages.FormatUnknownOption(arguments[index]));

				return ExitCodes.InvalidArguments;
			}
		}

		if (arguments.Length == 0 || (defaultTicket && ticketFile is not null))
		{
			ConsoleOutput.WriteError(CliMessages.InvalidArguments, CliMessages.MissingPrintCapabilitiesArguments);

			return ExitCodes.InvalidArguments;
		}

		IPrinter printer = await catalog.GetPrinterAsync(arguments[0], CancellationToken.None);

		if (printer is not IWindowsPrinter windowsPrinter)
		{
			ConsoleOutput.WriteError(CliMessages.InvalidArguments, CliMessages.FormatNotAWindowsPrinter(printer.Info.Id));

			return ExitCodes.InvalidArguments;
		}

		string document = defaultTicket
			? await windowsPrinter.GetDefaultPrintTicketAsync(CancellationToken.None)
			: await windowsPrinter.GetPrintCapabilitiesAsync
			(
				ticketFile is null ? null : await File.ReadAllTextAsync(ticketFile),
				CancellationToken.None
			);

		Console.Out.WriteLine(document);

		return ExitCodes.Success;
	}
}
