using Latiprimere.EndToEnd;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;

namespace Latiprimere.TestHarness.Commands;

/// <summary>
/// The <c>capture</c> command: saves a printer's IPP attributes, as the captures in
/// <c>tests/Latiprimere.Ipp.Tests/Data</c> are made, and lists the values to replace before sharing them. Only reads
/// the printer.
/// </summary>
internal static class CaptureCommand
{
	/// <summary>
	/// Describes the command, for the usage text.
	/// </summary>
	public static readonly string[] Usage =
	[
		"  capture <printer> [--name <file-name>]",
		"                       Saves a printer's IPP attributes (.ipp and .txt) in out/captures, without printing.",
		"                       <printer> is an ipp:// or ipps:// URI, an ipp: printer ID, or a cups: queue on",
		"                       CUPS_SERVER (or localhost:631)"
	];

	/// <summary>
	/// Captures the printer's attributes.
	/// </summary>
	/// <param name="arguments">The printer, then optionally <c>--name &lt;file-name&gt;</c>.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The exit code.</returns>
	/// <exception cref="ArgumentException">The printer is missing or has no IPP address.</exception>
	public static async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken)
	{
		if (arguments.Length == 0)
		{
			throw new ArgumentException("capture needs a printer: an ipp:// URI, an ipp: printer ID or a cups: queue.");
		}

		string printer = arguments[0];
		Uri? printerUri = Uri.TryCreate(printer, UriKind.Absolute, out Uri? uri) && uri.Scheme is "ipp" or "ipps"
			? uri
			: ManualPrinting.FindIppUri(printer);

		if (printerUri is null)
		{
			throw new ArgumentException($"'{printer}' has no IPP address to capture from: give an ipp:// URI, an ipp: printer ID or a cups: queue.");
		}

		int nameIndex = Array.IndexOf(arguments, "--name");
		string fileName = nameIndex >= 0 && nameIndex + 1 < arguments.Length
			? arguments[nameIndex + 1]
			: $"{printerUri.Host}-printer-attributes";

		string directory = Path.Combine(HarnessEnvironment.FindRepositoryRoot() ?? Directory.GetCurrentDirectory(), "out", "captures");
		Console.WriteLine($"Asking {printerUri} for its attributes...");

		try
		{
			IppCaptureResult result = await IppCapture.CaptureAsync(printerUri, directory, fileName, cancellationToken);
			Console.WriteLine($"{result.AttributeCount} attributes saved:");
			Console.WriteLine($"  {result.IppPath}");
			Console.WriteLine($"  {result.TextPath}");
			Console.WriteLine();
			Console.WriteLine("Before adding them to tests/Latiprimere.Ipp.Tests/Data, replace these values with placeholders of the same");
			Console.WriteLine("length (see that folder's README):");

			foreach (string value in result.IdentifyingValues)
			{
				Console.WriteLine($"  {value}");
			}

			return ExitCodes.Success;
		}
		catch (HttpRequestException exception)
		{
			Console.Error.WriteLine($"Couldn't get the attributes from {printerUri}: {exception.Message}");

			return ExitCodes.ScenarioFailed;
		}
	}
}
