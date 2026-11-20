using System;
using System.Net;
using System.Threading.Tasks;
using Latiprimere;
using Microsoft.Extensions.DependencyInjection;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Secure network printers".
/// </summary>
internal static class SecurePrinters
{
	/// <summary>The office printer's IPPS address, on the port it announces IPPS on.</summary>
	private const string OfficePrinterUri = "ipps://192.168.20.15:443/ipp/print";

	/// <summary>
	/// Registers a network printer reached over IPPS, with its own certificate pinned and its own password.
	/// </summary>
	/// <param name="fingerprint">The SHA-256 fingerprint of the printer's certificate, checked against the one its
	/// web page shows.</param>
	/// <param name="password">The printer's password, from a secret store.</param>
	/// <returns>The service provider, which the caller disposes.</returns>
	public static ServiceProvider BuildServiceProvider(string fingerprint, string password)
	{
		ServiceCollection services = new ServiceCollection();

		services.AddLatiprimere();
		services.AddLatiprimerePdfium();
		services.AddLatiprimereIpp(options =>
		{
			options.PrinterUris.Add(new Uri(SecurePrinters.OfficePrinterUri));

			// From the PrintCertificateException, checked against the printer's web page
			options.TrustedCertificateFingerprints.Add(fingerprint);

			// Sent to this printer only, and only over IPPS, never in clear text
			options.PrinterCredentials[new Uri(SecurePrinters.OfficePrinterUri)] = new NetworkCredential("office", password);
		});

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Looks a printer up, and if its certificate isn't trusted, shows the fingerprint to check before pinning it.
	/// </summary>
	/// <param name="catalog">The catalog.</param>
	/// <param name="printerId">The printer's ID.</param>
	/// <returns>The printer, or <see langword="null"/> if its certificate isn't trusted yet.</returns>
	public static async Task<IPrinter?> FindPrinterAsync(IPrinterCatalog catalog, string printerId)
	{
		try
		{
			return await catalog.GetPrinterAsync(printerId);
		}
		catch (PrintCertificateException exception)
		{
			// Show it, to be checked against the printer's own web page, before pinning it
			Console.WriteLine($"{exception.PrinterUri} presented a certificate with fingerprint {exception.CertificateFingerprint}.");

			return null;
		}
	}
}
