using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Connects to a host over IPv6 and IPv4 together, the "Happy Eyeballs" way (RFC 8305), and keeps whichever
/// connection opens first.
/// </summary>
/// <remarks>
/// <para>
/// .NET on its own tries a host's addresses one after another, each until it fails. A host whose name has an IPv6
/// address that doesn't answer, such as <c>localhost</c> in front of a server listening on IPv4 only, then costs
/// about 2 seconds per connection on Windows before IPv4 is tried.
/// </para>
/// <para>
/// Here the addresses alternate between IPv6 and IPv4, IPv6 first. The next attempt starts when the one before
/// fails, or after <see cref="DefaultAttemptDelay"/> while it is still trying; earlier attempts keep going. The first
/// connection to open wins, and the others are abandoned and closed.
/// </para>
/// </remarks>
internal sealed class HappyEyeballsConnector
{
	/// <summary>
	/// The default wait before starting the next attempt while the previous one is still trying: the "Connection
	/// Attempt Delay" RFC 8305 recommends.
	/// </summary>
	internal static readonly TimeSpan DefaultAttemptDelay = TimeSpan.FromMilliseconds(250);

	private readonly IHostResolver resolver;   // Looks up the host's addresses
	private readonly ITcpConnector connector;  // Connects to one address
	private readonly TimeSpan attemptDelay;    // Wait before starting the next attempt alongside a slow one
	private readonly TimeProvider timeProvider; // Clock for the waits

	/// <summary>
	/// Initialises a new instance of the <see cref="HappyEyeballsConnector"/> class.
	/// </summary>
	/// <param name="resolver">Looks up the host's addresses.</param>
	/// <param name="connector">Connects to one address.</param>
	/// <param name="attemptDelay">The wait before starting the next attempt while the previous one is still trying.</param>
	/// <param name="timeProvider">The clock for the waits.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="attemptDelay"/> is negative.</exception>
	public HappyEyeballsConnector(IHostResolver resolver, ITcpConnector connector, TimeSpan attemptDelay, TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		ArgumentNullException.ThrowIfNull(connector);
		ArgumentNullException.ThrowIfNull(timeProvider);
		ArgumentOutOfRangeException.ThrowIfLessThan(attemptDelay, TimeSpan.Zero);

		this.resolver = resolver;
		this.connector = connector;
		this.attemptDelay = attemptDelay;
		this.timeProvider = timeProvider;
	}

	/// <summary>
	/// Creates a connector that uses the system's resolver and sockets, with the recommended attempt delay.
	/// </summary>
	/// <returns>The connector.</returns>
	public static HappyEyeballsConnector CreateDefault()
	{
		return new HappyEyeballsConnector
		(
			new DnsHostResolver(),
			new TcpSocketConnector(),
			HappyEyeballsConnector.DefaultAttemptDelay,
			TimeProvider.System
		);
	}

	/// <summary>
	/// Puts addresses in the order to try them: alternating IPv6 and IPv4, IPv6 first, otherwise keeping the
	/// resolver's order.
	/// </summary>
	/// <param name="addresses">The resolved addresses.</param>
	/// <returns>The addresses in the order to try them.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="addresses"/> is <see langword="null"/>.</exception>
	internal static IReadOnlyList<IPAddress> OrderAddresses(IReadOnlyList<IPAddress> addresses)
	{
		ArgumentNullException.ThrowIfNull(addresses);

		List<IPAddress> ipv6 = addresses.Where(address => address.AddressFamily == AddressFamily.InterNetworkV6).ToList();
		List<IPAddress> others = addresses.Where(address => address.AddressFamily != AddressFamily.InterNetworkV6).ToList();
		List<IPAddress> ordered = new List<IPAddress>(addresses.Count);

		for (int index = 0; index < Math.Max(ipv6.Count, others.Count); index++)
		{
			if (index < ipv6.Count)
			{
				ordered.Add(ipv6[index]);
			}

			if (index < others.Count)
			{
				ordered.Add(others[index]);
			}
		}

		return ordered;
	}

	/// <summary>
	/// Opens a connection to a host and port.
	/// </summary>
	/// <param name="endPoint">The host name or address, and the port.</param>
	/// <param name="cancellationToken">A token to give up; <see cref="SocketsHttpHandler"/> cancels it when its
	/// connect timeout runs out.</param>
	/// <returns>A stream over the first connection to open. The caller owns it.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="endPoint"/> is <see langword="null"/>.</exception>
	/// <exception cref="SocketException">The name could not be resolved, or every address failed; then the first
	/// address's failure is thrown.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	public async ValueTask<Stream> ConnectAsync(DnsEndPoint endPoint, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(endPoint);

		IReadOnlyList<IPAddress> addresses = await this.ResolveAsync(endPoint.Host, cancellationToken).ConfigureAwait(false);

		// Attempts share a token of their own, so the losers can be abandoned once one wins
		using CancellationTokenSource attemptsCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		List<Task<Stream>> running = new List<Task<Stream>>();
		List<Exception> failures = new List<Exception>();
		int nextAddress = 0;

		try
		{
			while (true)
			{
				// Start the next address: at first, after a failure, or when the previous attempt is slow
				if (nextAddress < addresses.Count)
				{
					IPEndPoint target = new IPEndPoint(addresses[nextAddress], endPoint.Port);
					running.Add(this.connector.ConnectAsync(target, attemptsCancellation.Token));
					nextAddress++;
				}
				else if (running.Count == 0)
				{
					// Every address failed
					ExceptionDispatchInfo.Throw(failures[0]);
				}

				Task<Stream>? finished = await this.WaitForAttemptAsync(running, nextAddress < addresses.Count, cancellationToken)
					.ConfigureAwait(false);

				if (finished is null)
				{
					// Still trying, and there are more addresses: start the next one alongside
					continue;
				}

				running.Remove(finished);

				if (finished.IsCompletedSuccessfully)
				{
					return finished.Result;
				}

				// Failed: record why, and go straight on to the next address
				failures.Add(HappyEyeballsConnector.GetFailure(finished));
			}
		}
		finally
		{
			// Abandon the losers, closing any that connect before they notice
			await attemptsCancellation.CancelAsync().ConfigureAwait(false);

			foreach (Task<Stream> attempt in running)
			{
				_ = attempt.ContinueWith
				(
					HappyEyeballsConnector.ReleaseAttempt,
					CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously,
					TaskScheduler.Default
				);
			}
		}
	}

	/// <summary>
	/// Gets why an attempt failed.
	/// </summary>
	/// <param name="attempt">The failed or cancelled attempt.</param>
	/// <returns>The exception it ended with.</returns>
	private static Exception GetFailure(Task<Stream> attempt)
	{
		return attempt.Exception?.InnerException ?? new OperationCanceledException();
	}

	/// <summary>
	/// Closes an abandoned attempt's connection if it opened, or observes its failure so it isn't reported as
	/// unobserved.
	/// </summary>
	/// <param name="attempt">The finished attempt.</param>
	private static void ReleaseAttempt(Task<Stream> attempt)
	{
		if (attempt.IsCompletedSuccessfully)
		{
			attempt.Result.Dispose();
		}
		else
		{
			_ = attempt.Exception;
		}
	}

	/// <summary>
	/// Looks up the host's addresses, unless it is an address already, and puts them in the order to try them.
	/// </summary>
	/// <param name="host">The host name or address.</param>
	/// <param name="cancellationToken">A token to cancel the lookup.</param>
	/// <returns>The addresses to try, in order; never empty.</returns>
	/// <exception cref="SocketException">The name has no addresses, a <c>.local</c> name has none a printer may have,
	/// or the name could not be resolved.</exception>
	private async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
	{
		if (IPAddress.TryParse(host, out IPAddress? address))
		{
			return [address];
		}

		IPAddress[] addresses = await this.resolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);

		// Anyone on the network can answer for a .local name, with any address, so it is held to the addresses a
		// printer may have, as an announced address is (ADR 0058)
		if (PrinterAddressRules.IsMulticastDnsName(host))
		{
			addresses = addresses.Where(PrinterAddressRules.CanBePrinter).ToArray();
		}

		if (addresses.Length == 0)
		{
			throw new SocketException((int)SocketError.HostNotFound);
		}

		return HappyEyeballsConnector.OrderAddresses(addresses);
	}

	/// <summary>
	/// Waits for one of the running attempts to finish, or, when there are more addresses to try, at most the
	/// attempt delay.
	/// </summary>
	/// <param name="running">The attempts still trying.</param>
	/// <param name="hasMoreAddresses">Whether there are addresses not tried yet.</param>
	/// <param name="cancellationToken">A token to give up waiting.</param>
	/// <returns>The attempt that finished, or <see langword="null"/> if the attempt delay passed first.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	private async Task<Task<Stream>?> WaitForAttemptAsync
	(
		List<Task<Stream>> running,
		bool               hasMoreAddresses,
		CancellationToken  cancellationToken
	)
	{
		Task<Task<Stream>> anyFinished = Task.WhenAny(running);

		if (!hasMoreAddresses)
		{
			return await anyFinished.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		using CancellationTokenSource delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task delay = Task.Delay(this.attemptDelay, this.timeProvider, delayCancellation.Token);
		Task first = await Task.WhenAny(anyFinished, delay).ConfigureAwait(false);

		// Stop the timer if an attempt won the race, and surface the caller's cancellation if that ended the wait
		await delayCancellation.CancelAsync().ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();

		return first == anyFinished ? await anyFinished.ConfigureAwait(false) : null;
	}
}
