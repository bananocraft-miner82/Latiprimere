namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// A stream that can be read but can't seek or say how long it is, as a network stream can't.
/// </summary>
/// <param name="data">What it reads.</param>
internal sealed class NonSeekableStream(byte[] data) : MemoryStream(data, writable: false)
{
	/// <inheritdoc/>
	public override bool CanSeek
	{
		get
		{
			return false;
		}
	}

	/// <inheritdoc/>
	public override long Length
	{
		get
		{
			throw new NotSupportedException();
		}
	}

	/// <inheritdoc/>
	public override long Position
	{
		get
		{
			throw new NotSupportedException();
		}
		set
		{
			throw new NotSupportedException();
		}
	}
}
