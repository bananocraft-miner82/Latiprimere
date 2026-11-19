---
title: Printing from a web app or background service
description: Accept print requests in ASP.NET Core, queue them, and print them in a background service that follows each job.
sidebar:
  order: 7
---

Printing takes time: a printer may be busy, asleep or slow to answer. A web request shouldn't wait for that. This guide builds an ASP.NET Core app that accepts PDFs, puts them in a queue and answers straight away, while a background service prints them one at a time and logs what happened to each.

```text
POST /print ──▶ PrintQueue ──▶ PrintWorker ──▶ printer
   (202)       (in memory)    (one at a time)
```

## Register everything

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddLatiprimere();
builder.Services.AddLatiprimereCups();
builder.Services.AddLatiprimereWindows();
builder.Services.AddLatiprimerePdfium();
builder.Services.AddSingleton<PrintQueue>();
builder.Services.AddHostedService<PrintWorker>();

await using WebApplication app = builder.Build();

app.MapGet("/printers", WebService.ListPrintersAsync);
app.MapPost("/print", WebService.QueuePrintAsync);

await app.RunAsync();
```

## The endpoints

`GET /printers` lists the printers, so a client can offer a choice and send back the chosen printer's ID:

```csharp
static async Task<IResult> ListPrintersAsync(IPrinterCatalog catalog, CancellationToken cancellationToken)
{
	IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync(cancellationToken);

	// The state is sent by name, such as "Idle", rather than as a number
	return Results.Ok(printers.Select(printer => new { printer.Id, printer.Name, State = printer.State.ToString(), printer.IsDefault }));
}
```

`POST /print?printer=ID` checks the document, reads it and queues it. It answers `202 Accepted` once the request is queued, before anything is printed:

```csharp
static async Task<IResult> QueuePrintAsync(string printer, HttpRequest request, PrintQueue queue, CancellationToken cancellationToken)
{
	// Check the document before reading it
	if (request.ContentType?.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase) != true)
	{
		return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
	}

	if (request.ContentLength is not long length)
	{
		return Results.StatusCode(StatusCodes.Status411LengthRequired);
	}

	if (length > WebService.MaxDocumentBytes)
	{
		return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
	}

	using MemoryStream document = new MemoryStream();
	await request.Body.CopyToAsync(document, cancellationToken);

	PrintRequest printRequest = new PrintRequest { PrinterId = printer, Document = document.ToArray() };

	// A full queue means printing has fallen behind; the client can try again later
	return queue.TryEnqueue(printRequest)
		? Results.Accepted()
		: Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}
```

## The queue

A bounded channel holds requests between the endpoint and the worker. When it's full, new requests are turned away rather than using ever more memory:

```csharp
internal sealed class PrintQueue
{
	private const int Capacity = 100;

	private readonly Channel<PrintRequest> channel = Channel.CreateBounded<PrintRequest>(PrintQueue.Capacity);

	public bool TryEnqueue(PrintRequest request)
	{
		return this.channel.Writer.TryWrite(request);
	}

	public IAsyncEnumerable<PrintRequest> ReadAllAsync(CancellationToken cancellationToken)
	{
		return this.channel.Reader.ReadAllAsync(cancellationToken);
	}
}
```

`PrintRequest` is a record with the printer ID, the document's bytes and an optional job name.

## The worker

The worker reads requests as they arrive. For each one it looks up the printer, sends the document, and follows the job for up to two minutes. A failed request is logged, and the worker carries on with the next:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
	await foreach (PrintRequest request in this.queue.ReadAllAsync(stoppingToken))
	{
		// A failed request is logged, and the worker carries on with the next
		try
		{
			await this.PrintAsync(request, stoppingToken);
		}
		catch (PrintException exception)
		{
			PrintWorker.LogPrintFailed(this.logger, request.PrinterId, exception);
		}
		catch (NotSupportedException exception)
		{
			PrintWorker.LogPrintFailed(this.logger, request.PrinterId, exception);
		}
	}
}

private async Task PrintAsync(PrintRequest request, CancellationToken stoppingToken)
{
	// The printer ID came from outside, so it is looked up in the catalog, never trusted as an address
	IPrinter printer = await this.catalog.GetPrinterAsync(request.PrinterId, stoppingToken);

	using MemoryStream document = new MemoryStream(request.Document, writable: false);
	PrintOptions options = new PrintOptions { JobName = request.JobName };

	IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, stoppingToken);

	PrintWorker.LogSubmitted(this.logger, job.Id, printer.Info.Name);

	// Follow the job for a while; stopping the application stops following, not the job
	using CancellationTokenSource following = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
	following.CancelAfter(PrintWorker.FollowTimeout);

	try
	{
		await foreach (PrintJobStatus status in job.WatchStatusAsync(following.Token))
		{
			if (status.IsTerminal)
			{
				PrintWorker.LogFinished(this.logger, job.Id, status.State);
			}
		}
	}
	catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
	{
		PrintWorker.LogStillPrinting(this.logger, job.Id);
	}
}
```

The logging methods are `[LoggerMessage]` methods; the full class, with its constructor, is in [PrintWorker.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/PrintWorker.cs).

## Keep it safe

- **Printer IDs come from requests,** so leave `AllowAnyPrinterUri` (network printers) and `AllowAnyPrinterName` (Windows) off, as they are by default. Then an ID can only reach printers the catalog already knows, and a made-up address can't make your server connect anywhere else. See [Network printers over IPP](/docs/print-systems/network-printers/).
- **Check what you accept:** the endpoint refuses anything that isn't a PDF, and documents over 20 MB.
- **Require sign-in.** The example is open to anyone who can reach it. In a real app, add authentication and call `RequireAuthorization()` on the endpoints.

## Things to decide for your app

- **Queued requests are lost on restart.** The queue is in memory. For documents that must not be lost, such as invoices, keep requests in a database or message queue, and remove each one once it's printed.
- **One job at a time.** The worker waits for each job before starting the next, which keeps label and receipt printers in order. To print to several printers at once, run a worker per printer, or follow jobs without waiting.
- **Stopping the app** stops following jobs, but jobs already sent carry on printing.
- **Telling the client how it went.** Here the outcome is only logged. To report it, give each request an ID in the `202` response, record each job's final state, and add an endpoint to read it.

## Try it

From a clone of the repository:

```bash
dotnet run --project samples/Latiprimere.Examples -f net10.0 -- web
```

Then, in another terminal, list the printers and send a PDF to one of them:

```bash
curl http://localhost:5000/printers
curl -X POST "http://localhost:5000/print?printer=<printer-id>" -H "Content-Type: application/pdf" --data-binary @invoice.pdf
```

The worker's log shows each job being sent and finishing.

The code on this page is in [WebService.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/WebService.cs), [PrintQueue.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/PrintQueue.cs), [PrintWorker.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/PrintWorker.cs) and [PrintRequest.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/PrintRequest.cs).
