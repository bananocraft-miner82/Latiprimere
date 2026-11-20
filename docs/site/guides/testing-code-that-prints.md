---
title: Testing code that prints
description: Unit-test code that prints without a printer, using small fakes of IPrinterCatalog, IPrinter and IPrintJob.
sidebar:
  order: 8
---

Tests that print to a real printer are slow, waste paper and fail when the printer is off. You don't need one to test your own code. Latiprimere's types are interfaces, so a test can hand your code fakes instead: a catalog that holds made-up printers, printers that keep what they're sent, and jobs that report whatever states the test chooses.

This guide tests a class that prints invoices, with xUnit. The same fakes work with any test framework.

## The code under test

`InvoicePrinter` prints an invoice two-sided on the printer the user chose, falls back to the default printer if that one has gone, and waits for the job to finish:

```csharp
internal sealed class InvoicePrinter
{
	private readonly IPrinterCatalog catalog;

	public InvoicePrinter(IPrinterCatalog catalog)
	{
		ArgumentNullException.ThrowIfNull(catalog);

		this.catalog = catalog;
	}

	public TimeSpan PollInterval { get; init; } = PrintJobExtensions.DefaultPollInterval;

	public async Task<bool> PrintAsync(string printerId, string invoiceNumber, Stream invoicePdf, CancellationToken cancellationToken)
	{
		IPrinter printer = await this.FindPrinterAsync(printerId, cancellationToken);
		PrintOptions options = new PrintOptions
		{
			JobName = $"Invoice {invoiceNumber}",
			Duplex = DuplexMode.LongEdge,
		};

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(invoicePdf), options, cancellationToken);

		// Follow the job until it finishes, and keep the last thing it reported
		PrintJobStatus? finalStatus = null;

		await foreach (PrintJobStatus status in job.WatchStatusAsync(this.PollInterval, cancellationToken))
		{
			finalStatus = status;
		}

		return finalStatus?.State == PrintJobState.Completed;
	}

	private async Task<IPrinter> FindPrinterAsync(string printerId, CancellationToken cancellationToken)
	{
		try
		{
			return await this.catalog.GetPrinterAsync(printerId, cancellationToken);
		}
		catch (PrinterNotFoundException)
		{
			IPrinter? defaultPrinter = await this.catalog.GetDefaultPrinterAsync(cancellationToken);

			if (defaultPrinter is null)
			{
				throw;
			}

			return defaultPrinter;
		}
	}
}
```

Two things make it easy to test:

- **It depends on `IPrinterCatalog`, not on a print system.** In the app, dependency injection supplies the real catalog. In a test, you pass a fake.
- **How often it checks the job is a setting.** `WatchStatusAsync` waits half a second between checks by default. A test sets `PollInterval` to a millisecond, so it doesn't wait.

## The fakes

Three small classes stand in for Latiprimere. Copy them into your test project and change them as you need.

### A catalog

`FakePrinterCatalog` holds a fixed list of printers. Like the real catalog, it throws `PrinterNotFoundException` for an ID it doesn't know:

```csharp
internal sealed class FakePrinterCatalog : IPrinterCatalog
{
	private readonly List<FakePrinter> printers;

	public FakePrinterCatalog(params IEnumerable<FakePrinter> printers)
	{
		this.printers = printers.ToList();
	}

	public FakePrinter? DefaultPrinter { get; init; }

	public Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
	{
		IReadOnlyList<PrinterInfo> descriptions = this.printers.Select(printer => printer.Info).ToList();

		return Task.FromResult(descriptions);
	}

	public Task<IPrinter> GetPrinterAsync(string printerId, CancellationToken cancellationToken = default)
	{
		FakePrinter? printer = this.printers.Find(candidate => candidate.Info.Id == printerId);

		return printer is null
			? Task.FromException<IPrinter>(PrinterNotFoundException.FromPrinterId(printerId))
			: Task.FromResult<IPrinter>(printer);
	}

	public Task<IPrinter?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IPrinter?>(this.DefaultPrinter);
	}
}
```

### A printer

`FakePrinter` keeps a copy of everything it's sent, so a test can check the document and options, and finds its jobs again by ID. It reads the document straight away, as a real printer does, because your code may close the stream as soon as the job is sent. Set `JobStates` to script what its jobs report, or `Rejection` to make it turn jobs away:

```csharp
internal sealed class FakePrinter : IPrinter
{
	private readonly List<SubmittedJob> submittedJobs = new List<SubmittedJob>();
	private readonly List<FakePrintJob> jobs = new List<FakePrintJob>();

	public FakePrinter(string id, string name)
	{
		this.Info = new PrinterInfo { Id = id, Name = name, State = PrinterState.Idle };
	}

	public PrinterInfo Info { get; }

	public IReadOnlyList<PrintJobState> JobStates { get; init; } = [PrintJobState.Completed];

	public PrintException? Rejection { get; init; }

	public PrinterCapabilities Capabilities { get; init; } = new PrinterCapabilities();

	public IReadOnlyList<SubmittedJob> SubmittedJobs => this.submittedJobs;

	public async Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(options);

		if (this.Rejection is not null)
		{
			throw this.Rejection;
		}

		// Read the document now, as a real printer would: the caller may close its stream once the job is sent
		using MemoryStream document = new MemoryStream();
		await content.Content.CopyToAsync(document, cancellationToken);

		this.submittedJobs.Add(new SubmittedJob { Format = content.Format, Options = options, Document = document.ToArray() });

		string jobId = this.submittedJobs.Count.ToString(CultureInfo.InvariantCulture);
		FakePrintJob job = new FakePrintJob(jobId, this.Info.Id, this.JobStates);
		this.jobs.Add(job);

		return job;
	}

	public Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IPrintJob?>(this.jobs.Find(job => job.Id == jobId));
	}

	public Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult(this.Capabilities);
	}
}
```

`SubmittedJob` is a record with the `Format`, `Options` and `Document` bytes of each job.

### A job

`FakePrintJob` reports one scripted state for each status check, then stays on the last. Like a real job, it can be cancelled until it has finished:

```csharp
internal sealed class FakePrintJob : IPrintJob
{
	private readonly Queue<PrintJobState> upcomingStates;
	private PrintJobState currentState;

	public FakePrintJob(string id, string printerId, IReadOnlyList<PrintJobState> states)
	{
		ArgumentOutOfRangeException.ThrowIfZero(states.Count, nameof(states));

		this.Id = id;
		this.PrinterId = printerId;
		this.upcomingStates = new Queue<PrintJobState>(states);
		this.currentState = states[0];
	}

	public string Id { get; }

	public string PrinterId { get; }

	public Task<PrintJobStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		if (this.upcomingStates.Count > 0)
		{
			this.currentState = this.upcomingStates.Dequeue();
		}

		PrintJobStatus status = new PrintJobStatus
		{
			JobId = this.Id,
			State = this.currentState,
			Timestamp = DateTimeOffset.UtcNow,
		};

		return Task.FromResult(status);
	}

	public Task<bool> CancelAsync(CancellationToken cancellationToken = default)
	{
		bool hasFinished = this.currentState is PrintJobState.Completed or PrintJobState.Canceled or PrintJobState.Aborted;

		if (!hasFinished)
		{
			this.upcomingStates.Clear();
			this.currentState = PrintJobState.Canceled;
		}

		return Task.FromResult(!hasFinished);
	}
}
```

`IPrintJob.WatchStatusAsync` has a default implementation that calls `GetStatusAsync` at the interval given, so it works on the fake job as it does on a real one, through `IPrintJob`: call it on a variable of that type, or cast the fake to it. Make sure every script ends in a final state, `Completed`, `Canceled` or `Aborted`: a job that never finishes keeps a test waiting until it times out.

## The tests

Each test builds the printers it needs, runs `InvoicePrinter`, and checks what the fake printer was sent or what came back. Two helpers keep them short:

```csharp
private static InvoicePrinter CreateInvoicePrinter(FakePrinterCatalog catalog)
{
	return new InvoicePrinter(catalog) { PollInterval = TimeSpan.FromMilliseconds(1) };
}

private static async Task<bool> PrintInvoiceAsync(InvoicePrinter invoices, string printerId)
{
	using MemoryStream invoicePdf = new MemoryStream(InvoicePrinterTests.InvoicePdf, writable: false);

	return await invoices.PrintAsync(printerId, "1042", invoicePdf, TestContext.Current.CancellationToken);
}
```

The document is a few bytes rather than a real PDF: only the fake printer reads it.

### What was sent

```csharp
[Fact]
public async Task GivenAnInvoiceWhenPrintedThenSendsItAsATwoSidedPdfNamedAfterTheInvoice()
{
	// Arrange
	FakePrinter officeLaser = new FakePrinter("windows:Office Laser", "Office Laser");
	InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(new FakePrinterCatalog(officeLaser));

	// Act
	await InvoicePrinterTests.PrintInvoiceAsync(invoices, "windows:Office Laser");

	// Assert
	SubmittedJob job = Assert.Single(officeLaser.SubmittedJobs);
	Assert.Equal(DocumentFormat.Pdf, job.Format);
	Assert.Equal("Invoice 1042", job.Options.JobName);
	Assert.Equal(DuplexMode.LongEdge, job.Options.Duplex);
	Assert.Equal(InvoicePrinterTests.InvoicePdf, job.Document.ToArray());
}
```

### How the job ended

Script the job's states to test each outcome. Here the printer aborts the job partway through:

```csharp
[Fact]
public async Task GivenAJobThePrinterAbortsWhenPrintedThenReturnsFalse()
{
	// Arrange
	FakePrinter officeLaser = new FakePrinter("windows:Office Laser", "Office Laser")
	{
		JobStates = [PrintJobState.Processing, PrintJobState.Aborted],
	};
	InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(new FakePrinterCatalog(officeLaser));

	// Act
	bool printed = await InvoicePrinterTests.PrintInvoiceAsync(invoices, "windows:Office Laser");

	// Assert
	Assert.False(printed);
}
```

### A printer that has gone

Leave the saved printer out of the catalog to test the fallback:

```csharp
[Fact]
public async Task GivenTheSavedPrinterHasGoneWhenPrintedThenUsesTheDefaultPrinter()
{
	// Arrange — the catalog no longer has the saved printer, but has a default
	FakePrinter reception = new FakePrinter("windows:Reception", "Reception");
	FakePrinterCatalog catalog = new FakePrinterCatalog(reception) { DefaultPrinter = reception };
	InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(catalog);

	// Act
	bool printed = await InvoicePrinterTests.PrintInvoiceAsync(invoices, "windows:Office Laser");

	// Assert
	Assert.True(printed);
	Assert.Single(reception.SubmittedJobs);
}
```

### A printer that turns the job away

Set `Rejection` to the exception a print system would throw:

```csharp
[Fact]
public async Task GivenAPrinterThatTurnsTheJobAwayWhenPrintedThenThrowsPrintException()
{
	// Arrange
	FakePrinter officeLaser = new FakePrinter("windows:Office Laser", "Office Laser")
	{
		Rejection = new PrintException("The printer is out of paper."),
	};
	InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(new FakePrinterCatalog(officeLaser));

	// Act
	Func<Task> print = () => InvoicePrinterTests.PrintInvoiceAsync(invoices, "windows:Office Laser");

	// Assert
	await Assert.ThrowsAsync<PrintException>(print);
	Assert.Empty(officeLaser.SubmittedJobs);
}
```

The full test class also covers a job that completes, and the case where there's neither the saved printer nor a default.

## What fakes can't tell you

Fakes check your code's decisions, not printing itself. They can't tell you whether a printer accepts your PDF, honours two-sided printing, or reports states the way you expect. For that, print to a real printer, or a virtual one such as Microsoft Print to PDF or CUPS-PDF, in a separate set of tests you run by hand or on a machine set up for it. [Errors and troubleshooting](/docs/guides/errors-and-troubleshooting/) lists the exceptions worth testing your code against.

## Try it

From a clone of the repository:

```bash
dotnet test --project samples/Latiprimere.Examples.Tests
```

The code on this page is in [samples/Latiprimere.Examples.Tests](https://github.com/bananocraft-miner82/Latiprimere/tree/main/samples/Latiprimere.Examples.Tests): [InvoicePrinter.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples.Tests/InvoicePrinter.cs), [InvoicePrinterTests.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples.Tests/InvoicePrinterTests.cs) and the [fakes](https://github.com/bananocraft-miner82/Latiprimere/tree/main/samples/Latiprimere.Examples.Tests/Fakes).
