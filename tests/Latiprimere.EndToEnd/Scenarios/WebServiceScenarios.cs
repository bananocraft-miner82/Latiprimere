using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Latiprimere.EndToEnd.Support;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// "Printing from a web app or background service": the documentation's ASP.NET Core example, with its queue and
/// hosted print worker, run as the program a reader would run.
/// </summary>
public static class WebServiceScenarios
{
	/// <summary>
	/// The web example lists printers, queues PDFs, and prints them in the background, surviving a bad request.
	/// </summary>
	public static readonly Scenario WebService = new Scenario
	{
		Id = "samples.examples.web-service",
		Area = ScenarioArea.Samples,
		Title = "The web example lists printers over HTTP, refuses a non-PDF, and its background worker logs a failed job then prints the next one to Completed",
		Needs = TargetNeeds.CupsContainer,
		RunAsync = WebServiceScenarios.WebServiceAsync
	};

	/// <summary>
	/// How long the web example may take to start answering.
	/// </summary>
	private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

	/// <summary>
	/// How long its worker may take to print a job.
	/// </summary>
	private static readonly TimeSpan PrintTimeout = TimeSpan.FromSeconds(90);

	/// <summary>
	/// Runs <see cref="WebService"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WebServiceAsync(ScenarioContext context)
	{
		int port = WebServiceScenarios.GetFreePort();
		Uri baseUri = new Uri($"http://127.0.0.1:{port}/");
		string[] arguments = ["web", "--urls", baseUri.AbsoluteUri.TrimEnd('/')];
		await using RunningSample web = Samples.Start(context, Samples.Examples, arguments);

		try
		{
			using HttpClient client = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(30) };

			// Listing, once the service answers
			string? printers = await WebServiceScenarios.WaitForPrintersAsync(client, web, context.CancellationToken);
			context.Require("GET /printers answers", printers is not null, printers is null ? "no answer" : null);
			context.Check("it lists CUPS-PDF", printers!.Contains(TestPrinters.CupsPdf, StringComparison.Ordinal));

			// Only PDFs are queued
			using (StringContent text = new StringContent("not a PDF"))
			{
				using HttpResponseMessage refused = await client.PostAsync($"print?printer={Uri.EscapeDataString(TestPrinters.CupsPdf)}", text, context.CancellationToken);
				context.CheckEqual("POST /print with text is refused as 415", HttpStatusCode.UnsupportedMediaType, refused.StatusCode);
			}

			// A job for a printer that doesn't exist fails in the worker, which logs it and carries on
			byte[] pdf = await File.ReadAllBytesAsync(Samples.GetDocumentPath(TestDocuments.ThreePages), context.CancellationToken);
			HttpStatusCode badStatus = await WebServiceScenarios.PostPdfAsync(client, "nosuch:printer", pdf, context.CancellationToken);
			context.CheckEqual("POST /print for a missing printer is accepted (202) for the worker", HttpStatusCode.Accepted, badStatus);
			bool isFailureLogged = await web.WaitForOutputAsync("Couldn't print on nosuch:printer", WebServiceScenarios.PrintTimeout, context.CancellationToken);
			context.Check("the worker logs that it couldn't print it", isFailureLogged);

			HttpStatusCode goodStatus = await WebServiceScenarios.PostPdfAsync(client, TestPrinters.CupsPdf, pdf, context.CancellationToken);
			context.CheckEqual("POST /print for CUPS-PDF is accepted (202)", HttpStatusCode.Accepted, goodStatus);
			bool isCompleted = await web.WaitForOutputAsync("finished: Completed", WebServiceScenarios.PrintTimeout, context.CancellationToken);
			context.Check("the worker prints it and logs it Completed", isCompleted);
			context.Check("the service is still running", !web.HasExited);
		}
		finally
		{
			await Samples.SaveOutputAsync(context, Samples.Examples, arguments, new SampleRun(web.HasExited ? -1 : 0, web.Output));
		}
	}

	/// <summary>
	/// Asks for <c>/printers</c> until the service answers, it ends, or the time runs out.
	/// </summary>
	/// <param name="client">The HTTP client.</param>
	/// <param name="web">The running service.</param>
	/// <param name="cancellationToken">Cancels the wait.</param>
	/// <returns>The answer's body, or <see langword="null"/>.</returns>
	private static async Task<string?> WaitForPrintersAsync(HttpClient client, RunningSample web, CancellationToken cancellationToken)
	{
		DateTime deadline = DateTime.UtcNow + WebServiceScenarios.StartTimeout;

		while (DateTime.UtcNow < deadline && !web.HasExited)
		{
			try
			{
				using HttpResponseMessage response = await client.GetAsync("printers", cancellationToken);

				if (response.IsSuccessStatusCode)
				{
					return await response.Content.ReadAsStringAsync(cancellationToken);
				}
			}
			catch (HttpRequestException)
			{
				// Not listening yet
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
		}

		return null;
	}

	/// <summary>
	/// Posts a PDF to <c>/print</c>.
	/// </summary>
	/// <param name="client">The HTTP client.</param>
	/// <param name="printerId">The printer to print on.</param>
	/// <param name="pdf">The PDF.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The answer's status.</returns>
	private static async Task<HttpStatusCode> PostPdfAsync(HttpClient client, string printerId, byte[] pdf, CancellationToken cancellationToken)
	{
		using ByteArrayContent content = new ByteArrayContent(pdf);
		content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
		using HttpResponseMessage response = await client.PostAsync($"print?printer={Uri.EscapeDataString(printerId)}", content, cancellationToken);

		return response.StatusCode;
	}

	/// <summary>
	/// Finds a free loopback port for the service.
	/// </summary>
	/// <returns>The port.</returns>
	private static int GetFreePort()
	{
		using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		int port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();

		return port;
	}
}
