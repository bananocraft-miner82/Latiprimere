using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Latiprimere;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LatiprimereExamples;

/// <summary>
/// The web app from "Printing from a web app or background service": an endpoint that lists printers, and one that
/// accepts PDFs and queues them for <see cref="PrintWorker"/>.
/// </summary>
internal static class WebService
{
	/// <summary>
	/// The largest document accepted, in bytes.
	/// </summary>
	private const long MaxDocumentBytes = 20 * 1024 * 1024;

	/// <summary>
	/// Runs the web app until it is stopped.
	/// </summary>
	/// <param name="args">The command-line arguments, for the web app's own settings.</param>
	/// <returns>A task that completes when the web app has stopped.</returns>
	public static async Task RunAsync(string[] args)
	{
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
	}

	/// <summary>
	/// Lists the printers, so a client can offer a choice and send back the chosen printer's ID.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="cancellationToken">Cancelled if the client goes away.</param>
	/// <returns>The printers' IDs, names and states.</returns>
	private static async Task<IResult> ListPrintersAsync(IPrinterCatalog catalog, CancellationToken cancellationToken)
	{
		IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync(cancellationToken);

		// The state is sent by name, such as "Idle", rather than as a number
		return Results.Ok(printers.Select(printer => new { printer.Id, printer.Name, State = printer.State.ToString(), printer.IsDefault }));
	}

	/// <summary>
	/// Accepts a PDF in the request body and queues it for printing: <c>POST /print?printer=ID</c>.
	/// </summary>
	/// <param name="printer">The ID of the printer to print on.</param>
	/// <param name="request">The HTTP request, whose body is the PDF.</param>
	/// <param name="queue">The print queue.</param>
	/// <param name="cancellationToken">Cancelled if the client goes away.</param>
	/// <returns>202 Accepted once queued, or an error status.</returns>
	private static async Task<IResult> QueuePrintAsync(string printer, HttpRequest request, PrintQueue queue, CancellationToken cancellationToken)
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
}
