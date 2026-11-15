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
	private readonly HashSet<IPAddress> stubbornAddresses = new HashSet<IPAddress>(); // Answer even after cancellation
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
	/// <param name="ignoresCancellation">Whether the connection opens even if the attempt is cancelled first, as a
	/// socket can when cancellation comes too late.</param>
	/// <returns>This connector, for chaining.</returns>
	public FakeTcpConnector AnswerAfter(IPAddress address, TimeSpan delay, bool ignoresCancellation = false)
	{
		this.answerDelays[address] = delay;

		if (ignoresCancellation)
		{
			this.stubbornAddresses.Add(address);
		}

		return this;
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

		bool isRefused = this.refuseDelays.TryGetValue(address, out TimeSpan refuseDelay);
		bool isAnswered = this.answerDelays.TryGetValue(address, out TimeSpan answerDelay);
		TimeSpan delay = isRefused ? refuseDelay : isAnswered ? answerDelay : Timeout.InfiniteTimeSpan;
		CancellationToken waitToken = this.stubbornAddresses.Contains(address) ? CancellationToken.None : cancellationToken;

		try
		{
			await Task.Delay(delay, waitToken).ConfigureAwait(false);
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

		FakeConnectionStream connection = new FakeConnectionStream(address);

		lock (this.gate)
		{
			this.connections.Add(connection);
		}

		return connection;
	}
}
