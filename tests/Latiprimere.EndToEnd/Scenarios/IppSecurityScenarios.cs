using System.Net;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Printers that need a password or an encrypted connection, played by the scripted printer.
/// </summary>
public static class IppSecurityScenarios
{
	/// <summary>
	/// A printer that asks for a password is refused with an explanation. Direct IPP can't send one yet (Phase 3).
	/// </summary>
	public static readonly Scenario PasswordRequired = new Scenario
	{
		Id = "ipp.security.password-required",
		Area = ScenarioArea.Security,
		Title = "A printer that asks for a user name and password is reported with a PrintAuthenticationException, HTTP 401",
		Needs = TargetNeeds.None,
		RunAsync = IppSecurityScenarios.PasswordRequiredAsync
	};

	/// <summary>
	/// A printer with a self-signed certificate is refused with an explanation of the certificate.
	/// </summary>
	public static readonly Scenario UntrustedCertificate = new Scenario
	{
		Id = "ipp.security.untrusted-certificate",
		Area = ScenarioArea.Security,
		Title = "An ipps printer with a self-signed certificate is refused with a PrintException about the encrypted connection, before any request is sent",
		Needs = TargetNeeds.None,
		RunAsync = IppSecurityScenarios.UntrustedCertificateAsync
	};

	/// <summary>
	/// Runs <see cref="PasswordRequired"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PasswordRequiredAsync(ScenarioContext context)
	{
		FakePrinterOptions options = new FakePrinterOptions { RequiredCredentials = new NetworkCredential("latiprimere", "test-password") };
		await using FakePrinter fake = await FakePrinters.StartAsync(context, options);
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);

		PrintException? exception = null;

		try
		{
			await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		}
		catch (PrintException caught)
		{
			exception = caught;
		}

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Require("looking it up throws", exception is not null);
		context.Note($"{exception!.GetType().Name}: {exception.Message}");
		context.Check("it is a PrintAuthenticationException", exception is PrintAuthenticationException, exception.GetType().Name);
		context.CheckEqual("its error code is HTTP 401", (int?)401, exception.NativeErrorCode);
		context.Check("the printer was asked without credentials", fake.Requests.Count > 0 && fake.Requests.All(request => !request.HasAuthorization));
	}

	/// <summary>
	/// Runs <see cref="UntrustedCertificate"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task UntrustedCertificateAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { UseTls = true });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		context.CheckEqual("the printer's URI is ipps", "ipps", fake.PrinterUri.Scheme);

		PrintException? exception = null;

		try
		{
			await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		}
		catch (PrintException caught)
		{
			exception = caught;
		}

		context.Require("looking it up throws a PrintException", exception is not null);
		context.Note($"{exception!.GetType().Name}: {exception.Message}");
		context.Check("it explains the encrypted connection", exception.Message.Contains("encrypted connection", StringComparison.OrdinalIgnoreCase), exception.Message);
		context.Check("it isn't mistaken for an authentication problem", exception is not PrintAuthenticationException);
		context.CheckEqual("no request reached the printer", 0, fake.Requests.Count);
	}
}
