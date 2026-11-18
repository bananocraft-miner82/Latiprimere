using System.IO.Compression;
using System.IO.Pipelines;
using System.Runtime.ExceptionServices;
using Latiprimere.Rendering;
using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// A PDF sent as raster: each attempt renders and encodes the pages on a worker thread while they are uploaded, so
/// a whole page is never held, and a printer that was busy gets the document rendered again from the start
/// (ADR 0035).
/// </summary>
internal sealed class RasterJobDocument : IppJobDocument
{
	/// <summary>How much encoded data may wait for the upload before rendering pauses.</summary>
	private const int PauseBytes = 1024 * 1024;

	private readonly IRasterDocument document; // The opened PDF; the caller disposes it
	private readonly RasterJobPlan plan;       // How to encode it

	/// <summary>
	/// Initialises a new instance of the <see cref="RasterJobDocument"/> class.
	/// </summary>
	/// <param name="document">The opened PDF, rendered on one thread at a time; the caller disposes it.</param>
	/// <param name="plan">How to encode it.</param>
	public RasterJobDocument(IRasterDocument document, RasterJobPlan plan)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(plan);

		this.document = document;
		this.plan = plan;
	}

	/// <inheritdoc/>
	/// <remarks>Always <see langword="true"/>: rendering can be repeated.</remarks>
	public override bool CanResend
	{
		get
		{
			return true;
		}
	}

	/// <inheritdoc/>
	/// <remarks>When rendering or encoding fails, that failure is thrown, rather than the failed upload it caused.
	/// </remarks>
	public override async Task<PrintJobResponse> SendAsync
	(
		IIppClient          client,
		PrintJobRequest     request,
		IppVendorAttributes vendorAttributes,
		CancellationToken   cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(request);

		Pipe pipe = new Pipe
		(
			new PipeOptions
			(
				pauseWriterThreshold: RasterJobDocument.PauseBytes,
				resumeWriterThreshold: RasterJobDocument.PauseBytes / 2,
				useSynchronizationContext: false
			)
		);

		using CancellationTokenSource rendering = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CancellationToken renderingToken = rendering.Token;

		// Its own thread: it blocks while the upload catches up
		Task encoding = Task.Factory.StartNew
		(
			() => this.Encode(pipe.Writer, renderingToken),
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default
		);

		// Not disposed here: completing the pipe's reader, which every path below does, is all disposing it would do
		request.Document = pipe.Reader.AsStream();

		PrintJobResponse response;

		try
		{
			response = await client.PrintJobAsync(request, vendorAttributes, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// The upload failed: if rendering failed first, that is why, and is the error to report
			Exception? renderingFailure = await RasterJobDocument.StopAsync(rendering, pipe.Reader, encoding).ConfigureAwait(false);

			if (renderingFailure is not null)
			{
				ExceptionDispatchInfo.Throw(renderingFailure);
			}

			throw;
		}

		// The upload read it all, so rendering has finished; this only makes sure, should a client stop reading early
		await RasterJobDocument.StopAsync(rendering, pipe.Reader, encoding).ConfigureAwait(false);

		return response;
	}

	/// <summary>
	/// Stops rendering, if it is still going, and waits for it to end.
	/// </summary>
	/// <param name="rendering">Cancels the rendering.</param>
	/// <param name="reader">The upload's end of the pipe, completed so the rendering doesn't wait for it.</param>
	/// <param name="encoding">The rendering.</param>
	/// <returns>Why rendering failed, or <see langword="null"/> if it didn't fail by itself.</returns>
	private static async Task<Exception?> StopAsync(CancellationTokenSource rendering, PipeReader reader, Task encoding)
	{
		await rendering.CancelAsync().ConfigureAwait(false);
		await reader.CompleteAsync().ConfigureAwait(false);

		try
		{
			await encoding.ConfigureAwait(false);

			return null;
		}
		catch (OperationCanceledException)
		{
			return null;
		}
		catch (Exception exception)
		{
			return exception;
		}
	}

	/// <summary>
	/// Renders and encodes the document into the pipe, gzipped if the plan says so, then completes it; a failure
	/// completes it with the failure, so the upload stops.
	/// </summary>
	/// <param name="writer">The rendering's end of the pipe.</param>
	/// <param name="cancellationToken">A token that stops the rendering between bands.</param>
	private void Encode(PipeWriter writer, CancellationToken cancellationToken)
	{
		try
		{
			using (Stream pipe = writer.AsStream(leaveOpen: true))
			{
				Stream output = this.plan.Gzip ? new GZipStream(pipe, CompressionLevel.Fastest, leaveOpen: true) : pipe;

				using (output)
				{
					RasterDocumentEncoder.Encode(this.document, this.plan.Settings, output, cancellationToken);
				}
			}

			writer.Complete();
		}
		catch (Exception exception)
		{
			writer.Complete(exception);

			throw;
		}
	}
}
