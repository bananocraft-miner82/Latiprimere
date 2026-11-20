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
	/// A printer that asks for a password, when none is configured, is refused with an explanation.
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
		Title = "An ipps printer with a self-signed certificate is refused with a PrintException giving the certificate's SHA-256 fingerprint to pin, before any request is sent",
		Needs = TargetNeeds.None,
		RunAsync = IppSecurityScenarios.UntrustedCertificateAsync
	};

	/// <summary>
	/// A printer whose self-signed certificate is pinned prints over IPPS (ADR 0037).
	/// </summary>
	public static readonly Scenario PinnedCertificate = new Scenario
	{
		Id = "ipp.security.pinned-certificate",
		Area = ScenarioArea.Security,
		Title = "An ipps printer whose self-signed certificate's fingerprint is in TrustedCertificateFingerprints prints a PDF over the encrypted connection, and the job completes",
		Needs = TargetNeeds.None,
		RunAsync = IppSecurityScenarios.PinnedCertificateAsync
	};

	/// <summary>
	/// A printer that needs a password gets it over IPPS (ADR 0037).
	/// </summary>
	public static readonly Scenario PasswordOverIpps = new Scenario
	{
		Id = "ipp.security.password-over-ipps",
		Area = ScenarioArea.Security,
		Title = "An ipps printer that needs a user name and password, its certificate pinned, gets the configured credentials with every request, and the job completes",
		Needs = TargetNeeds.None,
		RunAsync = IppSecurityScenarios.PasswordOverIppsAsync
	};

	/// <summary>
	/// A password is never sent over plain IPP (ADR 0037).
	/// </summary>
	public static readonly Scenario PasswordNotSentInClear = new Scenario
	{
		Id = "ipp.security.password-not-sent-in-clear",
		Area = ScenarioArea.Security,
		Title = "A plain ipp printer that asks for a password doesn't get the configured one: the lookup fails with a PrintAuthenticationException saying to use its ipps address",
		Needs = TargetNeeds.None,
		RunAsync = IppSecurityScenarios.PasswordNotSentInClearAsync
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
		context.Check("it is a PrintCertificateException", exception is PrintCertificateException, exception.GetType().Name);
		context.CheckEqual("it gives the certificate's fingerprint to pin", fake.CertificateFingerprint, (exception as PrintCertificateException)?.CertificateFingerprint);
		context.Check("and the message gives it too", exception.Message.Contains(fake.CertificateFingerprint!, StringComparison.Ordinal), exception.Message);
		context.Check("and says where to pin it", exception.Message.Contains("TrustedCertificateFingerprints", StringComparison.Ordinal), exception.Message);
		context.Check("it isn't mistaken for an authentication problem", exception is not PrintAuthenticationException);
		context.CheckEqual("no request reached the printer", 0, fake.Requests.Count);
	}

	/// <summary>
	/// Runs <see cref="PinnedCertificate"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PinnedCertificateAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { UseTls = true });
		context.Note($"Its certificate's fingerprint: {fake.CertificateFingerprint}");

		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, options => options.TrustedCertificateFingerprints.Add(fake.CertificateFingerprint!));
		await IppSecurityScenarios.PrintAndCheckAsync(context, fake, fixture);
	}

	/// <summary>
	/// Runs <see cref="PasswordOverIpps"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PasswordOverIppsAsync(ScenarioContext context)
	{
		NetworkCredential credentials = new NetworkCredential("latiprimere", "test-password");
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { UseTls = true, RequiredCredentials = credentials });

		await using CatalogFixture fixture = FakePrinters.CreateCatalog
		(
			fake.PrinterUri,
			options =>
			{
				options.TrustedCertificateFingerprints.Add(fake.CertificateFingerprint!);
				options.Credentials = credentials;
			}
		);

		await IppSecurityScenarios.PrintAndCheckAsync(context, fake, fixture);
		context.Check("every request carried the credentials", fake.Requests.Count > 0 && fake.Requests.All(request => request.HasAuthorization));
	}

	/// <summary>
	/// Runs <see cref="PasswordNotSentInClear"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PasswordNotSentInClearAsync(ScenarioContext context)
	{
		NetworkCredential credentials = new NetworkCredential("latiprimere", "test-password");
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { RequiredCredentials = credentials });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, options => options.Credentials = credentials);
		context.CheckEqual("the printer's URI is plain ipp", "ipp", fake.PrinterUri.Scheme);

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
		context.Check("it says to use the ipps address", exception.Message.Contains("ipps://", StringComparison.Ordinal), exception.Message);
		context.Check("the password never left in clear text", fake.Requests.Count > 0 && fake.Requests.All(request => !request.HasAuthorization));
	}

	/// <summary>
	/// Looks the scripted printer up and prints a three-page PDF to it, checking the job completes over IPPS.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <param name="fake">The scripted printer.</param>
	/// <param name="fixture">The catalog.</param>
	/// <returns>A task that completes when the job has ended and been checked.</returns>
	private static async Task PrintAndCheckAsync(ScenarioContext context, FakePrinter fake, CatalogFixture fixture)
	{
		context.CheckEqual("the printer's URI is ipps", "ipps", fake.PrinterUri.Scheme);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);
		context.CheckEqual("the printer received one Print-Job", 1, fake.GetRequests(SharpIpp.Protocol.Models.IppOperation.PrintJob).Count);
	}
}
