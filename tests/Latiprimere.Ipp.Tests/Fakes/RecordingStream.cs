namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// A memory stream that records the size of each write it is given.
/// </summary>
internal sealed class RecordingStream : MemoryStream
{
	/// <summary>Gets the size of each write, in order.</summary>
	public List<int> WriteSizes { get; } = new List<int>();

	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		this.WriteSizes.Add(buffer.Length);
		base.Write(buffer);
	}

	/// <inheritdoc/>
	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		// MemoryStream's own WriteAsync calls Write, which would count the write twice
		this.WriteSizes.Add(buffer.Length);
		base.Write(buffer.Span);

		return ValueTask.CompletedTask;
	}
}
