using System.Net;
using System.Net.Sockets;
using Latiprimere.Ipp.Connections;

namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// Connects to configured addresses after configured delays, recording every attempt. Addresses that aren't
/// configured never answer, until the attempt is cancelled.
/// </summary>
internal sealed class FakeTcpConnector : ITcpConnector
{
	private readonly Lock gate = new Lock();                         // Guards the lists; attempts run concurrently
	private readonly Dictionary<IPAddress, TimeSpan> answerDelays = new Dictionary<IPAddress, TimeSpan>(); // Answering addresses
	private readonly Dictionary<IPAddress, TimeSpan> refuseDelays = new Dictionary<IPAddress, TimeSpan>(); // Refusing addresses
	private readonly Dictionary<IPAddress, TaskCompletionSource> releases = new Dictionary<IPAddress, TaskCompletionSource>(); // Answer when the test says
	private readonly List<IPAddress> attempts = new List<IPAddress>();               // Addresses tried, in order
	private readonly List<IPAddress> cancelledAttempts = new List<IPAddress>();      // Attempts abandoned
	private readonly List<FakeConnectionStream> connections = new List<FakeConnectionStream>(); // Connections opened

	/// <summary>Gets the addresses tried, in the order the attempts started.</summary>
	public IReadOnlyList<IPAddress> Attempts
	{
		get
		{
			lock (this.gate)
			{
				return this.attempts.ToList();
			}
		}
	}

	/// <summary>Gets the addresses whose attempts were cancelled.</summary>
	public IReadOnlyList<IPAddress> CancelledAttempts
	{
		get
		{
			lock (this.gate)
			{
				return this.cancelledAttempts.ToList();
			}
		}
	}

	/// <summary>Gets the connections opened, winners and losers.</summary>
	public IReadOnlyList<FakeConnectionStream> Connections
	{
		get
		{
			lock (this.gate)
			{
				return this.connections.ToList();
			}
		}
	}

	/// <summary>Makes an address accept connections after a delay.</summary>
	/// <param name="address">The address.</param>
	/// <param name="delay">How long the connection takes to open.</param>
	/// <returns>This connector, for chaining.</returns>
	public FakeTcpConnector AnswerAfter(IPAddress address, TimeSpan delay)
	{
		this.answerDelays[address] = delay;

		return this;
	}

	/// <summary>
	/// Makes an address accept a connection only when <see cref="Release"/> is called, even if the attempt has been
	/// cancelled by then, as a socket can when cancellation comes too late. Unlike a delay, the test decides when.
	/// </summary>
	/// <param name="address">The address.</param>
	/// <returns>This connector, for chaining.</returns>
	public FakeTcpConnector AnswerWhenReleased(IPAddress address)
	{
		this.releases[address] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		return this;
	}

	/// <summary>Lets an address set up with <see cref="AnswerWhenReleased"/> open its connection.</summary>
	/// <param name="address">The address.</param>
	public void Release(IPAddress address)
	{
		this.releases[address].SetResult();
	}

	/// <summary>Makes an address refuse connections after a delay.</summary>
	/// <param name="address">The address.</param>
	/// <param name="delay">How long the refusal takes.</param>
	/// <returns>This connector, for chaining.</returns>
	public FakeTcpConnector RefuseAfter(IPAddress address, TimeSpan delay)
	{
		this.refuseDelays[address] = delay;

		return this;
	}

	/// <inheritdoc/>
	public async Task<Stream> ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
	{
		IPAddress address = endPoint.Address;

		lock (this.gate)
		{
			this.attempts.Add(address);
		}

		if (this.releases.TryGetValue(address, out TaskCompletionSource? release))
		{
			await release.Task.ConfigureAwait(false);

			return this.Open(address);
		}

		bool isRefused = this.refuseDelays.TryGetValue(address, out TimeSpan refuseDelay);
		bool isAnswered = this.answerDelays.TryGetValue(address, out TimeSpan answerDelay);
		TimeSpan delay = isRefused ? refuseDelay : isAnswered ? answerDelay : Timeout.InfiniteTimeSpan;

		try
		{
			await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			lock (this.gate)
			{
				this.cancelledAttempts.Add(address);
			}

			throw;
		}

		if (isRefused)
		{
			throw new SocketException((int)SocketError.ConnectionRefused);
		}

		return this.Open(address);
	}

	/// <summary>
	/// Opens a connection to an address, and records it.
	/// </summary>
	/// <param name="address">The address.</param>
	/// <returns>The connection.</returns>
	private FakeConnectionStream Open(IPAddress address)
	{
		FakeConnectionStream connection = new FakeConnectionStream(address);

		lock (this.gate)
		{
			this.connections.Add(connection);
		}

		return connection;
	}
}
