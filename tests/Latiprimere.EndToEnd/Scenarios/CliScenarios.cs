using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// The command-line sample (<c>samples/Latiprimere.Cli</c>), run as a program against the scripted printer, which
/// records what reaches it.
/// </summary>
public static class CliScenarios
{
	/// <summary>
	/// The CLI lists, describes and prints to an IPP printer, and reports a refusal with its exit code.
	/// </summary>
	public static readonly Scenario AgainstFakePrinter = new Scenario
	{
		Id = "samples.cli.ipp-printer",
		Area = ScenarioArea.Samples,
		Title = "The CLI lists an IPP printer, shows its capabilities, prints with paper, tray, colour and a vendor option that all reach the printer, exits with 1 when the printer refuses a value, and with 64 for a printer address that isn't one",
		Needs = TargetNeeds.None,
		RunAsync = CliScenarios.AgainstFakePrinterAsync
	};

	/// <summary>
	/// The CLI prints to a printer over IPPS once its certificate's fingerprint is listed, and says how to list it.
	/// </summary>
	public static readonly Scenario AgainstSecurePrinter = new Scenario
	{
		Id = "samples.cli.ipps-printer",
		Area = ScenarioArea.Samples,
		Title = "The CLI refuses an ipps:// printer's self-signed certificate with exit code 1, giving the fingerprint and LATIPRIMERE_IPP_FINGERPRINTS to list it in; with it listed, prints and shows the job's reasons; and exits with 64 for a fingerprint that isn't one",
		Needs = TargetNeeds.None,
		RunAsync = CliScenarios.AgainstSecurePrinterAsync
	};

	/// <summary>
	/// The CLI's exit code when printing fails (<c>ExitCodes.PrintFailed</c>).
	/// </summary>
	private const int PrintFailed = 1;

	/// <summary>
	/// The CLI's exit code for arguments or settings it can't use (<c>ExitCodes.InvalidArguments</c>).
	/// </summary>
	private const int InvalidArguments = 64;

	/// <summary>
	/// Runs <see cref="AgainstFakePrinter"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task AgainstFakePrinterAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		string printerId = FakePrinters.GetPrinterId(fake);
		string document = Samples.GetDocumentPath(TestDocuments.ThreePages);

		// The CLI looks up the printers this variable lists, besides those it discovers
		Dictionary<string, string?> environment = new Dictionary<string, string?> { ["LATIPRIMERE_IPP_PRINTERS"] = fake.PrinterUri.AbsoluteUri };

		SampleRun list = await Samples.RunAsync(context, Samples.Cli, ["list"], environment);
		context.CheckEqual("'list' exits with 0", 0, list.ExitCode);
		context.Check("'list' shows the IPP printer", list.Output.Contains(printerId, StringComparison.Ordinal));

		SampleRun capabilities = await Samples.RunAsync(context, Samples.Cli, ["caps", printerId], environment);
		context.CheckEqual("'caps' exits with 0", 0, capabilities.ExitCode);
		context.Check("'caps' shows the manual tray", capabilities.Output.Contains("(manual)", StringComparison.Ordinal));

		string[] print = ["print", printerId, document, "--paper", "iso_a4_210x297mm", "--tray", "manual", "--color", "mono", "--option", "ipp:print-quality=5"];
		SampleRun printed = await Samples.RunAsync(context, Samples.Cli, print, environment);
		context.CheckEqual("'print' exits with 0", 0, printed.ExitCode);
		context.Check("'print' reports Completed", printed.Output.Contains("Completed", StringComparison.Ordinal));

		RecordedRequest? job = fake.GetRequests(IppOperation.PrintJob).FirstOrDefault(request => request.StatusCode == IppStatusCode.SuccessfulOk);
		string mediaCol = job?.Find("media-col")?.Value ?? string.Empty;
		context.Check("the printer gets A4 from the manual tray", mediaCol.Contains("media-source=manual", StringComparison.Ordinal) && mediaCol.Contains("x-dimension=21000", StringComparison.Ordinal), mediaCol);
		context.CheckEqual("the printer gets monochrome", "monochrome", job?.Find("print-color-mode")?.Value);
		context.CheckEqual("the printer gets print-quality 5", "5", job?.Find("print-quality")?.Value);

		SampleRun refused = await Samples.RunAsync(context, Samples.Cli, ["print", printerId, document, "--option", "ipp:print-quality=99"], environment);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("a value the printer refuses exits with 1", CliScenarios.PrintFailed, refused.ExitCode);
		context.Check("and shows the printer's status", refused.Output.Contains("0x040B", StringComparison.Ordinal));

		// A mistyped address in the variable is reported, not a crash
		Dictionary<string, string?> badAddress = new Dictionary<string, string?> { ["LATIPRIMERE_IPP_PRINTERS"] = "192.168.1.20/ipp/print" };
		SampleRun invalid = await Samples.RunAsync(context, Samples.Cli, ["list"], badAddress);
		context.CheckEqual("an address that isn't an ipp:// URI exits with 64", CliScenarios.InvalidArguments, invalid.ExitCode);
		context.Check("and says which value is wrong", invalid.Output.Contains("'192.168.1.20/ipp/print'", StringComparison.Ordinal));
	}

	/// <summary>
	/// Runs <see cref="AgainstSecurePrinter"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task AgainstSecurePrinterAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { UseTls = true });
		string printerId = FakePrinters.GetPrinterId(fake);
		string fingerprint = fake.CertificateFingerprint!;
		string document = Samples.GetDocumentPath(TestDocuments.ThreePages);
		context.CheckEqual("the printer's URI is ipps", "ipps", fake.PrinterUri.Scheme);

		Dictionary<string, string?> untrusted = new Dictionary<string, string?> { ["LATIPRIMERE_IPP_PRINTERS"] = fake.PrinterUri.AbsoluteUri };
		SampleRun refused = await Samples.RunAsync(context, Samples.Cli, ["print", printerId, document], untrusted);
		context.CheckEqual("an untrusted certificate exits with 1", CliScenarios.PrintFailed, refused.ExitCode);
		context.Check("and says to list its fingerprint in LATIPRIMERE_IPP_FINGERPRINTS", refused.Output.Contains("LATIPRIMERE_IPP_FINGERPRINTS=" + fingerprint, StringComparison.Ordinal), refused.Output);

		Dictionary<string, string?> trusted = new Dictionary<string, string?>
		{
			["LATIPRIMERE_IPP_PRINTERS"]     = fake.PrinterUri.AbsoluteUri,
			["LATIPRIMERE_IPP_FINGERPRINTS"] = fingerprint
		};

		SampleRun printed = await Samples.RunAsync(context, Samples.Cli, ["print", printerId, document], trusted);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("with it listed, 'print' exits with 0", 0, printed.ExitCode);
		context.Check("and reports Completed", printed.Output.Contains("Completed", StringComparison.Ordinal));
		context.Check("with the job's reasons", printed.Output.Contains("[job-completed-successfully]", StringComparison.Ordinal), printed.Output);
		context.CheckEqual("the printer got the job once", 1, fake.GetRequests(IppOperation.PrintJob).Count);

		Dictionary<string, string?> malformed = new Dictionary<string, string?>
		{
			["LATIPRIMERE_IPP_PRINTERS"]     = fake.PrinterUri.AbsoluteUri,
			["LATIPRIMERE_IPP_FINGERPRINTS"] = "3A:7F"
		};

		SampleRun invalid = await Samples.RunAsync(context, Samples.Cli, ["list"], malformed);
		context.CheckEqual("a fingerprint that isn't one exits with 64", CliScenarios.InvalidArguments, invalid.ExitCode);
		context.Check("and says which value is wrong", invalid.Output.Contains("'3A:7F'", StringComparison.Ordinal), invalid.Output);
	}
}
