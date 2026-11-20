using Latiprimere.Ipp.Connections;
using Latiprimere.Ipp.Tests.Fakes;
using Xunit;

namespace Latiprimere.Ipp.Tests.Connections;

public sealed class StallGuardedStreamTests
{
	[Fact]
	public async Task GivenLargeWriteWhenWrittenAsyncThenPassedOnInPiecesWithEveryByte()
	{
		// Arrange — one write of three pieces and 7 bytes, as SharpIppNext writes a seekable document
		byte[] content = StallGuardedStreamTests.CreateContent((3 * StallGuardedStream.MaxWriteSize) + 7);
		using RecordingStream inner = new RecordingStream();
		using IppStallWatch watch = new IppStallWatch(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
		using StallGuardedStream stream = new StallGuardedStream(inner, watch);

		// Act
		await stream.WriteAsync(content, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal([65536, 65536, 65536, 7], inner.WriteSizes);
		Assert.Equal(content, inner.ToArray());
		Assert.False(watch.Token.IsCancellationRequested);
	}

	[Fact]
	public void GivenLargeWriteWhenWrittenThenPassedOnInPiecesWithEveryByte()
	{
		// Arrange
		byte[] content = StallGuardedStreamTests.CreateContent((64 * 1024) + 1);
		using RecordingStream inner = new RecordingStream();
		using IppStallWatch watch = new IppStallWatch(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
		using StallGuardedStream stream = new StallGuardedStream(inner, watch);

		// Act
		stream.Write(content, 0, content.Length);

		// Assert
		Assert.Equal([65536, 1], inner.WriteSizes);
		Assert.Equal(content, inner.ToArray());
	}

	[Fact]
	public async Task GivenSmallWriteWhenWrittenAsyncThenPassedOnInOne()
	{
		// Arrange
		byte[] content = StallGuardedStreamTests.CreateContent(100);
		using RecordingStream inner = new RecordingStream();
		using IppStallWatch watch = new IppStallWatch(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
		using StallGuardedStream stream = new StallGuardedStream(inner, watch);

		// Act
		await stream.WriteAsync(content, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal([100], inner.WriteSizes);
	}

	/// <summary>
	/// Creates content whose bytes differ, so pieces in the wrong order would show.
	/// </summary>
	/// <param name="length">The length.</param>
	/// <returns>The content.</returns>
	private static byte[] CreateContent(int length)
	{
		byte[] content = new byte[length];

		for (int index = 0; index < length; index++)
		{
			content[index] = (byte)(index % 251);
		}

		return content;
	}
}
