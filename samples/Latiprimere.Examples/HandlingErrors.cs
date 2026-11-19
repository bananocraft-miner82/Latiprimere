using System;
using System.IO;
using System.Threading.Tasks;
using Latiprimere;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Errors and troubleshooting".
/// </summary>
internal static class HandlingErrors
{
	/// <summary>
	/// Prints a document, turning each kind of failure into a message for the user.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="printerId">The printer's ID.</param>
	/// <param name="document">The PDF to print.</param>
	/// <returns><see langword="true"/> if the job was accepted; otherwise <see langword="false"/>.</returns>
	public static async Task<bool> TryPrintAsync(IPrinterCatalog catalog, string printerId, Stream document)
	{
		try
		{
			IPrinter printer = await catalog.GetPrinterAsync(printerId);
			IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default);

			Console.WriteLine($"Sent as job {job.Id}");

			return true;
		}
		catch (PrinterNotFoundException exception)
		{
			Console.WriteLine($"There's no printer with the ID {exception.PrinterId}.");
		}
		catch (PrintAuthenticationException)
		{
			Console.WriteLine("The print server needs a user name and password.");
		}
		catch (PrintSystemUnavailableException exception)
		{
			Console.WriteLine($"Printing isn't available: {exception.Message}");
		}
		catch (NotSupportedException exception)
		{
			Console.WriteLine($"This printer can't print that: {exception.Message}");
		}
		catch (PrintException exception)
		{
			Console.WriteLine($"Printing failed: {exception.Message} (error code {exception.NativeErrorCode})");
		}

		return false;
	}
}
