using System.Net;

namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// A request body written in chunks with a pause before each, as a PDF sent as raster is written while its pages
/// render.
/// </summary>
internal sealed class PausingContent : HttpContent
{
	private readonly int chunks;     // The number of chunks
	private readonly int chunkSize;  // The bytes in each chunk
	private readonly TimeSpan pause; // The pause before each chunk

	/// <summary>
	/// Initialises a new instance of the <see cref="PausingContent"/> class.
	/// </summary>
	/// <param name="chunks">The number of chunks.</param>
	/// <param name="chunkSize">The bytes in each chunk.</param>
	/// <param name="pause">The pause before each chunk.</param>
	public PausingContent(int chunks, int chunkSize, TimeSpan pause)
	{
		this.chunks = chunks;
		this.chunkSize = chunkSize;
		this.pause = pause;
	}

	/// <inheritdoc/>
	protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
	{
		return this.SerializeToStreamAsync(stream, context, CancellationToken.None);
	}

	/// <inheritdoc/>
	protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
	{
		byte[] chunk = new byte[this.chunkSize];

		for (int index = 0; index < this.chunks; index++)
		{
			await Task.Delay(this.pause, cancellationToken);
			await stream.WriteAsync(chunk, cancellationToken);
		}
	}

	/// <inheritdoc/>
	protected override bool TryComputeLength(out long length)
	{
		length = 0;

		return false;
	}
}
