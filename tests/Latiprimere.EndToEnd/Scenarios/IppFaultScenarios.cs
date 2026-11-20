using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Printers that are busy, slow, unreachable or hang up, played by the scripted printer.
/// </summary>
public static class IppFaultScenarios
{
	/// <summary>
	/// A printer busy with other jobs gets the job once it is free (ADR 0017).
	/// </summary>
	public static readonly Scenario BusyThenAccepts = new Scenario
	{
		Id = "ipp.faults.busy-then-accepts",
		Area = ScenarioArea.Faults,
		Title = "A printer that is busy twice gets the job on the third attempt, each time with the whole document",
		Needs = TargetNeeds.None,
		RunAsync = IppFaultScenarios.BusyThenAcceptsAsync
	};

	/// <summary>
	/// A printer busy for longer than the retry duration ends the job with the busy error.
	/// </summary>
	public static readonly Scenario BusyPastLimit = new Scenario
	{
		Id = "ipp.faults.busy-past-limit",
		Area = ScenarioArea.Faults,
		Title = "A printer still busy when the retry duration (4 s here) runs out is reported with server-error-busy",
		Needs = TargetNeeds.None,
		RunAsync = IppFaultScenarios.BusyPastLimitAsync
	};

	/// <summary>
	/// A printer that hangs up during Print-Job is reported, not waited on, and the job isn't sent twice.
	/// </summary>
	public static readonly Scenario DroppedConnection = new Scenario
	{
		Id = "ipp.faults.dropped-connection",
		Area = ScenarioArea.Faults,
		Title = "A printer that closes the connection after receiving a job is reported as a PrintException, and the job isn't sent again",
		Needs = TargetNeeds.None,
		RunAsync = IppFaultScenarios.DroppedConnectionAsync
	};

	/// <summary>
	/// A printer that stops reading a job partway through fails the submission once the stall timeout runs out,
	/// rather than holding it forever.
	/// </summary>
	public static readonly Scenario StalledUpload = new Scenario
	{
		Id = "ipp.faults.stalled-upload",
		Area = ScenarioArea.Faults,
		Title = "A printer that stops reading a job partway through fails the submission with a PrintException once the upload stall timeout (2 s here) runs out, and the job isn't sent again",
		Needs = TargetNeeds.None,
		RunAsync = IppFaultScenarios.StalledUploadAsync
	};

	/// <summary>
	/// A printer slow to answer still prints the job and is followed to the end.
	/// </summary>
	public static readonly Scenario SlowPrinter = new Scenario
	{
		Id = "ipp.faults.slow-printer",
		Area = ScenarioArea.Faults,
		Title = "A printer that takes a second to answer every request still gets the job, followed to Completed",
		Needs = TargetNeeds.None,
		RunAsync = IppFaultScenarios.SlowPrinterAsync
	};

	/// <summary>
	/// A configured printer that is switched off fails quickly and doesn't break the listing.
	/// </summary>
	public static readonly Scenario SwitchedOff = new Scenario
	{
		Id = "ipp.faults.switched-off",
		Area = ScenarioArea.Faults,
		Title = "A configured printer that is switched off doesn't stop the catalog listing, and looking it up fails with a PrintException within the connect timeout",
		Needs = TargetNeeds.None,
		RunAsync = IppFaultScenarios.SwitchedOffAsync
	};

	/// <summary>
	/// A printer named <c>localhost</c> is reached quickly whether it listens on IPv4 or IPv6 only (RFC 8305).
	/// </summary>
	public static readonly Scenario DualStack = new Scenario
	{
		Id = "ipp.faults.dual-stack",
		Area = ScenarioArea.Faults,
		Title = "A printer named localhost is reached within 1.5 s on IPv4 only and on IPv6 only, for each family localhost resolves to here, and refused quickly on one it doesn't",
		Needs = TargetNeeds.None,
		RunAsync = IppFaultScenarios.DualStackAsync
	};

	/// <summary>
	/// The IPP status of a busy printer.
	/// </summary>
	private const int ServerErrorBusy = 0x0507;

	/// <summary>
	/// The longest a lookup by <c>localhost</c> may take: the 250 ms Happy Eyeballs delay and the request, with room
	/// for a slow machine. Before Happy Eyeballs, an IPv4-only printer took about 2 s on Windows.
	/// </summary>
	private static readonly TimeSpan DualStackLimit = TimeSpan.FromSeconds(1.5);

	/// <summary>
	/// <see cref="DualStackLimit"/> as check names show it, the same on every machine.
	/// </summary>
	private static readonly string DualStackLimitText = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{IppFaultScenarios.DualStackLimit.TotalSeconds} s");

	/// <summary>
	/// Runs <see cref="BusyThenAccepts"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task BusyThenAcceptsAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		fake.Behaviour.BusyPrintJobs = 2;
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		byte[] pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", TestDocuments.ThreePages), context.CancellationToken);
		using MemoryStream document = new MemoryStream(pdf);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		IReadOnlyList<RecordedRequest> attempts = fake.GetRequests(IppOperation.PrintJob);
		context.CheckEqual("the job is sent three times", 3, attempts.Count);
		context.Check("the first two are turned away as busy", attempts.Take(2).All(attempt => attempt.StatusCode == IppStatusCode.ServerErrorBusy));
		context.Check("every attempt carries the whole document", attempts.All(attempt => attempt.Document.AsSpan().SequenceEqual(pdf)));
	}

	/// <summary>
	/// Runs <see cref="BusyPastLimit"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task BusyPastLimitAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		fake.Behaviour.BusyPrintJobs = int.MaxValue;
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, options => options.BusyRetryDuration = TimeSpan.FromSeconds(4));
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		Stopwatch stopwatch = Stopwatch.StartNew();
		PrintException? exception = await IppFaultScenarios.CatchAsync(() => printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken));
		stopwatch.Stop();
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.Require("submitting throws a PrintException", exception is not null);
		context.Note("Message: " + exception!.Message);
		context.CheckEqual("its error code is server-error-busy", (int?)IppFaultScenarios.ServerErrorBusy, exception.NativeErrorCode);
		context.Check("the job was tried more than once", fake.GetRequests(IppOperation.PrintJob).Count > 1, $"{fake.GetRequests(IppOperation.PrintJob).Count} attempts");
		context.Check("it gave up within the retry duration and one more wait", stopwatch.Elapsed < TimeSpan.FromSeconds(12), $"{stopwatch.Elapsed.TotalSeconds:0.0} s");
	}

	/// <summary>
	/// Runs <see cref="DroppedConnection"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task DroppedConnectionAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		fake.Behaviour.DropConnectionOnPrintJob = true;
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		Stopwatch stopwatch = Stopwatch.StartNew();
		PrintException? exception = await IppFaultScenarios.CatchAsync(() => printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken));
		stopwatch.Stop();
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.Require("submitting throws a PrintException", exception is not null);
		context.Note($"{exception!.GetType().Name}: {exception.Message}");
		context.Check("it isn't mistaken for an authentication problem", exception is not PrintAuthenticationException);
		context.CheckEqual("the job is sent once, not resent", 1, fake.GetRequests(IppOperation.PrintJob).Count);
		context.Check("it is reported at once rather than waited on", stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"{stopwatch.Elapsed.TotalSeconds:0.0} s");
	}

	/// <summary>
	/// Runs <see cref="StalledUpload"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task StalledUploadAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		fake.Behaviour.StallPrintJobUpload = true;
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, options => options.UploadStallTimeout = TimeSpan.FromSeconds(2));
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		// Far more than the printer's and the network's buffers hold, so the upload can't finish unless the printer reads
		using MemoryStream data = new MemoryStream(new byte[64 * 1024 * 1024]);

		Stopwatch stopwatch = Stopwatch.StartNew();
		PrintException? exception = await IppFaultScenarios.CatchAsync(() => printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, context.CancellationToken));
		stopwatch.Stop();
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.Require("submitting throws a PrintException", exception is not null);
		context.Note($"{exception!.GetType().Name}: {exception.Message}");
		context.Check("it says the printer stopped taking the document", exception.Message.Contains("stopped taking the document", StringComparison.Ordinal), exception.Message);
		context.CheckEqual("the printer was sent the job once, not resent", 1, fake.StalledPrintJobs);
		context.Check("it gave up soon after the 2 s stall timeout", stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"{stopwatch.Elapsed.TotalSeconds:0.0} s");
	}

	/// <summary>
	/// Runs <see cref="SlowPrinter"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task SlowPrinterAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		fake.Behaviour.ResponseDelay = TimeSpan.FromSeconds(1);
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);
		context.CheckEqual("the printer got the job once", 1, fake.GetRequests(IppOperation.PrintJob).Count);
	}

	/// <summary>
	/// Runs <see cref="SwitchedOff"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task SwitchedOffAsync(ScenarioContext context)
	{
		// A printer that was there a moment ago, so its port is known to be free now
		Uri printerUri;

		await using (FakePrinter fake = await FakePrinters.StartAsync(context))
		{
			printerUri = fake.PrinterUri;
		}

		await using CatalogFixture fixture = FakePrinters.CreateCatalog(printerUri);
		string printerId = TestPrinters.GetIppPrinterId(printerUri);

		Exception? listingError = null;
		IReadOnlyList<PrinterInfo> printers = [];

		try
		{
			printers = await fixture.Catalog.GetPrintersAsync(context.CancellationToken);
		}
		catch (PrintException caught)
		{
			listingError = caught;
		}

		context.Check("listing printers doesn't throw", listingError is null, listingError?.Message);
		context.Note("Listed: " + (printers.Count == 0 ? "nothing" : string.Join(", ", printers.Select(printer => $"{printer.Id} ({printer.State})"))));

		Stopwatch stopwatch = Stopwatch.StartNew();
		PrintException? exception = await IppFaultScenarios.CatchAsync(() => fixture.Catalog.GetPrinterAsync(printerId, context.CancellationToken));
		stopwatch.Stop();

		context.Require("looking it up throws a PrintException", exception is not null);
		context.Note($"{exception!.GetType().Name}: {exception.Message}");
		context.Check("it fails within the 10 s connect timeout", stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"{stopwatch.Elapsed.TotalSeconds:0.0} s");
	}

	/// <summary>
	/// Runs <see cref="DualStack"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task DualStackAsync(ScenarioContext context)
	{
		// Windows resolves localhost to both ::1 and 127.0.0.1; Ubuntu's /etc/hosts gives it 127.0.0.1 only, naming ::1
		// ip6-localhost. A printer is only reachable by name on a family the name resolves to
		IPAddress[] addresses = await Dns.GetHostAddressesAsync("localhost", context.CancellationToken);
		context.Note("localhost resolves to " + string.Join(", ", addresses.Select(address => address.ToString())));

		foreach (FakePrinterAddress address in new[] { FakePrinterAddress.IPv4Loopback, FakePrinterAddress.IPv6Loopback })
		{
			AddressFamily family = address == FakePrinterAddress.IPv4Loopback ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;
			string label = address == FakePrinterAddress.IPv4Loopback ? "IPv4 only" : "IPv6 only";
			bool isReachable = addresses.Any(resolved => resolved.AddressFamily == family);

			FakePrinterOptions options = new FakePrinterOptions { Address = address, UriHost = "localhost" };
			await using FakePrinter fake = await FakePrinters.StartAsync(context, options);
			await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
			string printerId = FakePrinters.GetPrinterId(fake);

			Stopwatch stopwatch = Stopwatch.StartNew();
			PrintException? failure = null;
			IPrinter? printer = null;

			try
			{
				printer = await fixture.Catalog.GetPrinterAsync(printerId, context.CancellationToken);
			}
			catch (PrintException exception)
			{
				failure = exception;
			}

			stopwatch.Stop();
			string elapsed = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{stopwatch.Elapsed.TotalMilliseconds:0} ms");

			if (isReachable)
			{
				context.Check($"a printer on {label} is found", printer?.Info.Id == printerId, failure?.Message ?? printer?.Info.Id);
				context.Check($"within {IppFaultScenarios.DualStackLimitText} on {label}", stopwatch.Elapsed < IppFaultScenarios.DualStackLimit, elapsed);
			}
			else
			{
				context.Check($"a printer on {label}, which localhost doesn't resolve to here, is refused with a PrintException", failure is not null, failure?.Message ?? "it was reached");
				context.Check($"within {IppFaultScenarios.DualStackLimitText}, rather than waiting", stopwatch.Elapsed < IppFaultScenarios.DualStackLimit, elapsed);
			}
		}
	}

	/// <summary>
	/// Runs an action that should fail, and returns what it threw.
	/// </summary>
	/// <param name="action">The action.</param>
	/// <returns>The <see cref="PrintException"/> it threw, or <see langword="null"/> if it succeeded.</returns>
	private static async Task<PrintException?> CatchAsync(Func<Task> action)
	{
		try
		{
			await action();

			return null;
		}
		catch (PrintException exception)
		{
			return exception;
		}
	}
}
