using System.Net;

namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// The stream <see cref="FakeTcpConnector"/> returns for a connection, recording where it goes and whether it was
/// closed.
/// </summary>
internal sealed class FakeConnectionStream : MemoryStream
{
	/// <summary>
	/// Initialises a new instance of the <see cref="FakeConnectionStream"/> class.
	/// </summary>
	/// <param name="address">The address the connection went to.</param>
	public FakeConnectionStream(IPAddress address)
	{
		this.Address = address;
	}

	/// <summary>Gets the address the connection went to.</summary>
	public IPAddress Address { get; }

	/// <summary>Gets a value indicating whether the stream was disposed.</summary>
	public bool IsDisposed { get; private set; }

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		this.IsDisposed = true;
		base.Dispose(disposing);
	}
}
