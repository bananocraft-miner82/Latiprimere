using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Latiprimere;
using Microsoft.Extensions.DependencyInjection;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "CUPS servers and the Windows spooler".
/// </summary>
internal static class CupsAndWindows
{
	/// <summary>
	/// Registers CUPS on a print server that asks for a password and uses its own self-signed certificate.
	/// </summary>
	/// <param name="password">The print service account's password, from a secret store.</param>
	/// <param name="serverThumbprint">The SHA-256 thumbprint of the server's certificate, in hexadecimal.</param>
	/// <returns>The service provider, which the caller disposes.</returns>
	public static ServiceProvider BuildServiceProviderForPrintServer(string password, string serverThumbprint)
	{
		ServiceCollection services = new ServiceCollection();

		services.AddLatiprimere();
		services.AddLatiprimereCups(options =>
		{
			options.Server = "printserver.example.com:631";
			options.Credentials = new NetworkCredential("print-service", password);

			// Trust this server's own certificate, and no other
			options.ServerCertificateValidationCallback = (_, certificate, _, _) =>
				certificate is not null
				&& string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), serverThumbprint, StringComparison.OrdinalIgnoreCase);
		});

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Registers the Windows spooler so it can also print to printers on print servers the user hasn't connected
	/// to. Only for applications whose printer IDs never come from outside.
	/// </summary>
	/// <returns>The service provider, which the caller disposes.</returns>
	public static ServiceProvider BuildServiceProviderForAnyWindowsPrinter()
	{
		ServiceCollection services = new ServiceCollection();

		services.AddLatiprimere();
		services.AddLatiprimereWindows(options => options.AllowAnyPrinterName = true);
		services.AddLatiprimerePdfium();

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Prints a document to a file with Microsoft Print to PDF, which works in a service with no user interface.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="document">The PDF to print.</param>
	/// <param name="outputPath">The file to write.</param>
	/// <returns>A task that completes when the file has been written.</returns>
	public static async Task SaveWithPrintToPdfAsync(IPrinterCatalog catalog, Stream document, string outputPath)
	{
		IPrinter printer = await catalog.GetPrinterAsync("windows:Microsoft Print to PDF");
		PrintOptions options = new PrintOptions { OutputFile = outputPath };

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options);

		await foreach (PrintJobStatus status in job.WatchStatusAsync())
		{
			Console.WriteLine(status.State);
		}
	}
}
