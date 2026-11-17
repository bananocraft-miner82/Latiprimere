using System.Globalization;
using System.Net;
using Latiprimere.FakeIppPrinter;

namespace Latiprimere.TestHarness.Commands;

/// <summary>
/// The <c>fake-printer</c> command: runs the scripted IPP printer on its own until Ctrl+C, showing each request, so
/// the CLI sample, a Windows queue or <c>ipptool</c> can be pointed at it by hand.
/// </summary>
internal static class FakePrinterCommand
{
	/// <summary>
	/// The port used unless <c>--port</c> says otherwise: next to <c>ippeveprinter</c>'s 8631.
	/// </summary>
	private const int DefaultPort = 8632;

	/// <summary>
	/// Describes the options, for the usage text.
	/// </summary>
	public static readonly string[] Usage =
	[
		"  fake-printer [options]  Runs the scripted IPP printer until Ctrl+C, showing each request:",
		"      --port <n>             Port to listen on (default 8632)",
		"      --profile <name>       Printer attributes: ippeveprinter (default) or hp-envy-5640",
		"      --tls                  ipps only, with a self-signed certificate",
		"      --password <u>:<p>     Require this user name and password (HTTP Basic)",
		"      --ipv6                 Listen on ::1 instead of 127.0.0.1",
		"      --any                  Listen on every IPv4 address, so containers and other machines can reach it",
		"      --busy <n>             Turn the next n jobs away as busy",
		"      --delay <seconds>      Wait before every answer",
		"      --processing <seconds> Keep each job processing this long",
		"      --stopped [reason]     Start stopped (printer-state-reasons, default media-empty-error)",
		"      --abort [message]      End jobs aborted (job-state-message, default 'Paper jam')",
		"      --drop                 Close the connection on every Print-Job",
		"      --no-validation        Accept every job attribute, supported or not"
	];

	/// <summary>
	/// Runs the printer.
	/// </summary>
	/// <param name="arguments">The options.</param>
	/// <param name="cancellationToken">Stops the printer.</param>
	/// <returns>The exit code.</returns>
	/// <exception cref="ArgumentException">An option isn't understood.</exception>
	public static async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken)
	{
		FakePrinterSettings settings = FakePrinterCommand.Parse(arguments);
		FakePrinterProfile profile = await FakePrinterProfile.LoadBuiltInAsync(settings.ProfileName, cancellationToken);

		FakePrinterOptions options = new FakePrinterOptions
		{
			Profile = profile,
			Port = settings.Port,
			Address = settings.Address,
			UriHost = settings.Address == FakePrinterAddress.AnyIPv4 ? Dns.GetHostName() : null,
			UseTls = settings.UseTls,
			RequiredCredentials = settings.Credentials,
			RequestRecorded = request => Console.WriteLine($"{request.ReceivedAt.ToLocalTime():HH:mm:ss} {request}")
		};

		await using FakePrinter printer = await FakePrinter.StartAsync(options, cancellationToken);
		settings.ApplyTo(printer.Behaviour);

		Console.WriteLine($"Fake IPP printer ({profile.Name}) at {printer.PrinterUri}");
		Console.WriteLine($"Try it with: LATIPRIMERE_IPP_PRINTERS={printer.PrinterUri} dotnet run --project samples/Latiprimere.Cli -- list");
		Console.WriteLine("Press Ctrl+C to stop.");

		try
		{
			await Task.Delay(Timeout.Infinite, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Ctrl+C: stop the printer and end normally
		}

		Console.WriteLine($"Stopped after {printer.Requests.Count} requests.");

		return ExitCodes.Success;
	}

	/// <summary>
	/// Reads the options.
	/// </summary>
	/// <param name="arguments">The options.</param>
	/// <returns>The settings.</returns>
	/// <exception cref="ArgumentException">An option isn't understood or lacks its value.</exception>
	private static FakePrinterSettings Parse(string[] arguments)
	{
		FakePrinterSettings settings = new FakePrinterSettings { Port = FakePrinterCommand.DefaultPort };
		int index = 0;

		while (index < arguments.Length)
		{
			string option = arguments[index++];

			switch (option)
			{
				case "--port":
					settings.Port = FakePrinterCommand.ReadInteger(arguments, ref index, option);
					break;

				case "--profile":
					settings.ProfileName = FakePrinterCommand.ReadValue(arguments, ref index, option);
					break;

				case "--tls":
					settings.UseTls = true;
					break;

				case "--password":
					string[] parts = FakePrinterCommand.ReadValue(arguments, ref index, option).Split(':', 2);
					settings.Credentials = parts.Length == 2
						? new NetworkCredential(parts[0], parts[1])
						: throw new ArgumentException("--password needs <user>:<password>.");
					break;

				case "--ipv6":
					settings.Address = FakePrinterAddress.IPv6Loopback;
					break;

				case "--any":
					settings.Address = FakePrinterAddress.AnyIPv4;
					break;

				case "--busy":
					settings.BusyPrintJobs = FakePrinterCommand.ReadInteger(arguments, ref index, option);
					break;

				case "--delay":
					settings.ResponseDelay = TimeSpan.FromSeconds(FakePrinterCommand.ReadInteger(arguments, ref index, option));
					break;

				case "--processing":
					settings.ProcessingTime = TimeSpan.FromSeconds(FakePrinterCommand.ReadInteger(arguments, ref index, option));
					break;

				case "--stopped":
					settings.StoppedReason = FakePrinterCommand.ReadOptionalValue(arguments, ref index) ?? "media-empty-error";
					break;

				case "--abort":
					settings.AbortMessage = FakePrinterCommand.ReadOptionalValue(arguments, ref index) ?? "Paper jam";
					break;

				case "--drop":
					settings.DropConnection = true;
					break;

				case "--no-validation":
					settings.ValidateJobs = false;
					break;

				default:
					throw new ArgumentException($"Unknown fake-printer option '{option}'.");
			}
		}

		return settings;
	}

	/// <summary>
	/// Reads an option's value.
	/// </summary>
	/// <param name="arguments">The options.</param>
	/// <param name="index">The value's index; moved past it.</param>
	/// <param name="option">The option, for the error message.</param>
	/// <returns>The value.</returns>
	/// <exception cref="ArgumentException">The value is missing.</exception>
	private static string ReadValue(string[] arguments, ref int index, string option)
	{
		return index < arguments.Length ? arguments[index++] : throw new ArgumentException($"{option} needs a value.");
	}

	/// <summary>
	/// Reads an option's whole-number value.
	/// </summary>
	/// <param name="arguments">The options.</param>
	/// <param name="index">The value's index; moved past it.</param>
	/// <param name="option">The option, for the error message.</param>
	/// <returns>The number.</returns>
	/// <exception cref="ArgumentException">The value is missing or isn't a whole number.</exception>
	private static int ReadInteger(string[] arguments, ref int index, string option)
	{
		string value = FakePrinterCommand.ReadValue(arguments, ref index, option);

		return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number >= 0
			? number
			: throw new ArgumentException($"{option} needs a whole number, not '{value}'.");
	}

	/// <summary>
	/// Reads an option's value if the next argument isn't another option.
	/// </summary>
	/// <param name="arguments">The options.</param>
	/// <param name="index">The value's index; moved past it if there is one.</param>
	/// <returns>The value, or <see langword="null"/>.</returns>
	private static string? ReadOptionalValue(string[] arguments, ref int index)
	{
		return index < arguments.Length && !arguments[index].StartsWith("--", StringComparison.Ordinal) ? arguments[index++] : null;
	}
}
