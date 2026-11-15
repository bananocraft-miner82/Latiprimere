using System.Net;
using System.Net.Sockets;
using Latiprimere.Ipp.Connections;
using Latiprimere.Ipp.Tests.Fakes;
using Xunit;

namespace Latiprimere.Ipp.Tests.Connections;

public sealed class HappyEyeballsConnectorTests
{
	private static readonly IPAddress Ipv6First  = IPAddress.Parse("2001:db8::1"); // First IPv6 address of the host
	private static readonly IPAddress Ipv6Second = IPAddress.Parse("2001:db8::2"); // Second IPv6 address of the host
	private static readonly IPAddress Ipv4First  = IPAddress.Parse("192.0.2.1");   // First IPv4 address of the host
	private static readonly IPAddress Ipv4Second = IPAddress.Parse("192.0.2.2");   // Second IPv4 address of the host

	private static readonly DnsEndPoint Printer = new DnsEndPoint("printer.example", 631); // The host connected to

	private static readonly TimeSpan Long = TimeSpan.FromSeconds(30); // Longer than any test should take

	[Fact]
	public void GivenMixedAddressesWhenOrderedThenAlternatesStartingWithIpv6()
	{
		// Arrange
		IPAddress[] addresses =
		[
			HappyEyeballsConnectorTests.Ipv4First,
			HappyEyeballsConnectorTests.Ipv4Second,
			HappyEyeballsConnectorTests.Ipv6First,
			HappyEyeballsConnectorTests.Ipv6Second,
		];

		// Act
		IReadOnlyList<IPAddress> ordered = HappyEyeballsConnector.OrderAddresses(addresses);

		// Assert
		IPAddress[] expected =
		[
			HappyEyeballsConnectorTests.Ipv6First,
			HappyEyeballsConnectorTests.Ipv4First,
			HappyEyeballsConnectorTests.Ipv6Second,
			HappyEyeballsConnectorTests.Ipv4Second,
		];

		Assert.Equal(expected, ordered);
	}

	[Fact]
	public void GivenOnlyIpv4AddressesWhenOrderedThenKeepsResolverOrder()
	{
		// Arrange
		IPAddress[] addresses = [HappyEyeballsConnectorTests.Ipv4Second, HappyEyeballsConnectorTests.Ipv4First];

		// Act
		IReadOnlyList<IPAddress> ordered = HappyEyeballsConnector.OrderAddresses(addresses);

		// Assert
		Assert.Equal(addresses, ordered);
	}

	[Fact]
	public async Task GivenFirstAddressAnswersWhenConnectThenUsesItWithoutTryingOthers()
	{
		// Arrange
		FakeTcpConnector tcp = new FakeTcpConnector().AnswerAfter(HappyEyeballsConnectorTests.Ipv6First, TimeSpan.Zero);
		HappyEyeballsConnector connector = HappyEyeballsConnectorTests.CreateConnector(tcp, HappyEyeballsConnectorTests.Long);

		// Act
		Stream stream = await connector.ConnectAsync(HappyEyeballsConnectorTests.Printer, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(HappyEyeballsConnectorTests.Ipv6First, Assert.IsType<FakeConnectionStream>(stream).Address);
		Assert.Equal([HappyEyeballsConnectorTests.Ipv6First], tcp.Attempts);
	}

	[Fact]
	public async Task GivenIpv6NeverAnswersWhenConnectThenIpv4StartsAfterDelayAndWinsAndIpv6IsAbandoned()
	{
		// Arrange — the case that motivated this: localhost in front of a server listening on IPv4 only
		FakeTcpConnector tcp = new FakeTcpConnector().AnswerAfter(HappyEyeballsConnectorTests.Ipv4First, TimeSpan.Zero);
		HappyEyeballsConnector connector = HappyEyeballsConnectorTests.CreateConnector(tcp, TimeSpan.FromMilliseconds(50));

		// Act
		Stream stream = await connector.ConnectAsync(HappyEyeballsConnectorTests.Printer, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(HappyEyeballsConnectorTests.Ipv4First, Assert.IsType<FakeConnectionStream>(stream).Address);
		Assert.Equal([HappyEyeballsConnectorTests.Ipv6First, HappyEyeballsConnectorTests.Ipv4First], tcp.Attempts);

		bool isIpv6Abandoned = await HappyEyeballsConnectorTests.EventuallyAsync
		(
			() => tcp.CancelledAttempts.Contains(HappyEyeballsConnectorTests.Ipv6First)
		);

		Assert.True(isIpv6Abandoned);
	}

	[Fact]
	public async Task GivenSlowIpv6WhenAttemptDelayHasNotPassedThenIpv4IsNotStartedYet()
	{
		// Arrange — IPv6 answers well within the attempt delay, so IPv4 should never be tried
		FakeTcpConnector tcp = new FakeTcpConnector()
			.AnswerAfter(HappyEyeballsConnectorTests.Ipv6First, TimeSpan.FromMilliseconds(100))
			.AnswerAfter(HappyEyeballsConnectorTests.Ipv4First, TimeSpan.Zero);

		HappyEyeballsConnector connector = HappyEyeballsConnectorTests.CreateConnector(tcp, HappyEyeballsConnectorTests.Long);

		// Act
		Stream stream = await connector.ConnectAsync(HappyEyeballsConnectorTests.Printer, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(HappyEyeballsConnectorTests.Ipv6First, Assert.IsType<FakeConnectionStream>(stream).Address);
		Assert.Equal([HappyEyeballsConnectorTests.Ipv6First], tcp.Attempts);
	}

	[Fact]
	public async Task GivenIpv6RefusedWhenConnectThenIpv4StartsAtOnceWithoutWaitingForTheDelay()
	{
		// Arrange — with a 30 s attempt delay, waiting for it would time the test out
		FakeTcpConnector tcp = new FakeTcpConnector()
			.RefuseAfter(HappyEyeballsConnectorTests.Ipv6First, TimeSpan.Zero)
			.AnswerAfter(HappyEyeballsConnectorTests.Ipv4First, TimeSpan.Zero);

		HappyEyeballsConnector connector = HappyEyeballsConnectorTests.CreateConnector(tcp, HappyEyeballsConnectorTests.Long);
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(10));

		// Act
		Stream stream = await connector.ConnectAsync(HappyEyeballsConnectorTests.Printer, deadline.Token);

		// Assert
		Assert.Equal(HappyEyeballsConnectorTests.Ipv4First, Assert.IsType<FakeConnectionStream>(stream).Address);
	}

	[Fact]
	public async Task GivenEveryAddressRefusesWhenConnectThenThrowsTheFirstAddressFailure()
	{
		// Arrange
		FakeTcpConnector tcp = new FakeTcpConnector()
			.RefuseAfter(HappyEyeballsConnectorTests.Ipv6First, TimeSpan.Zero)
			.RefuseAfter(HappyEyeballsConnectorTests.Ipv4First, TimeSpan.Zero)
			.RefuseAfter(HappyEyeballsConnectorTests.Ipv6Second, TimeSpan.Zero)
			.RefuseAfter(HappyEyeballsConnectorTests.Ipv4Second, TimeSpan.Zero);

		HappyEyeballsConnector connector = HappyEyeballsConnectorTests.CreateConnector(tcp, HappyEyeballsConnectorTests.Long);

		// Act
		Func<Task> connect = async () =>
		{
			await connector.ConnectAsync(HappyEyeballsConnectorTests.Printer, TestContext.Current.CancellationToken);
		};

		// Assert
		SocketException exception = await Assert.ThrowsAsync<SocketException>(connect);
		Assert.Equal(SocketError.ConnectionRefused, exception.SocketErrorCode);
		Assert.Equal(4, tcp.Attempts.Count);
	}

	[Fact]
	public async Task GivenCallerCancelsWhenNoAddressAnswersThenThrowsAndAbandonsEveryAttempt()
	{
		// Arrange — nothing configured, so no address ever answers. The caller cancels once all four attempts have
		// started, not after a fixed time: on a busy CI runner, 200 ms once wasn't enough for the 10 ms staggers
		FakeTcpConnector tcp = new FakeTcpConnector();
		HappyEyeballsConnector connector = HappyEyeballsConnectorTests.CreateConnector(tcp, TimeSpan.FromMilliseconds(10));
		using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		Task<Stream> connecting = connector.ConnectAsync(HappyEyeballsConnectorTests.Printer, cancellation.Token).AsTask();
		Assert.True(await HappyEyeballsConnectorTests.EventuallyAsync(() => tcp.Attempts.Count == 4));

		// Act
		await cancellation.CancelAsync();

		// Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting);
		Assert.True(await HappyEyeballsConnectorTests.EventuallyAsync(() => tcp.CancelledAttempts.Count == 4));
		Assert.Equal(4, tcp.Attempts.Count);
	}

	[Fact]
	public async Task GivenLoserConnectsAfterWinnerWhenConnectThenLoserConnectionIsClosed()
	{
		// Arrange — IPv6 opens after IPv4 has won, despite being abandoned
		FakeTcpConnector tcp = new FakeTcpConnector()
			.AnswerAfter(HappyEyeballsConnectorTests.Ipv6First, TimeSpan.FromMilliseconds(300), ignoresCancellation: true)
			.AnswerAfter(HappyEyeballsConnectorTests.Ipv4First, TimeSpan.Zero);

		HappyEyeballsConnector connector = HappyEyeballsConnectorTests.CreateConnector(tcp, TimeSpan.FromMilliseconds(50));

		// Act
		Stream stream = await connector.ConnectAsync(HappyEyeballsConnectorTests.Printer, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(HappyEyeballsConnectorTests.Ipv4First, Assert.IsType<FakeConnectionStream>(stream).Address);

		bool isLoserClosed = await HappyEyeballsConnectorTests.EventuallyAsync
		(
			() => tcp.Connections.Any(connection => connection.Address.Equals(HappyEyeballsConnectorTests.Ipv6First) && connection.IsDisposed)
		);

		Assert.True(isLoserClosed);
		Assert.False(Assert.IsType<FakeConnectionStream>(stream).IsDisposed);
	}

	[Fact]
	public async Task GivenAddressLiteralWhenConnectThenConnectsWithoutLookingItUp()
	{
		// Arrange
		FakeHostResolver resolver = new FakeHostResolver(HappyEyeballsConnectorTests.Ipv6First);
		FakeTcpConnector tcp = new FakeTcpConnector().AnswerAfter(HappyEyeballsConnectorTests.Ipv4Second, TimeSpan.Zero);
		HappyEyeballsConnector connector = new HappyEyeballsConnector(resolver, tcp, HappyEyeballsConnectorTests.Long, TimeProvider.System);

		// Act
		Stream stream = await connector.ConnectAsync(new DnsEndPoint("192.0.2.2", 631), TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(HappyEyeballsConnectorTests.Ipv4Second, Assert.IsType<FakeConnectionStream>(stream).Address);
		Assert.Equal(0, resolver.LookupCount);
	}

	[Fact]
	public async Task GivenNameWithNoAddressesWhenConnectThenThrowsHostNotFound()
	{
		// Arrange
		HappyEyeballsConnector connector = new HappyEyeballsConnector
		(
			new FakeHostResolver(),
			new FakeTcpConnector(),
			HappyEyeballsConnectorTests.Long,
			TimeProvider.System
		);

		// Act
		Func<Task> connect = async () =>
		{
			await connector.ConnectAsync(HappyEyeballsConnectorTests.Printer, TestContext.Current.CancellationToken);
		};

		// Assert
		SocketException exception = await Assert.ThrowsAsync<SocketException>(connect);
		Assert.Equal(SocketError.HostNotFound, exception.SocketErrorCode);
	}

	/// <summary>
	/// Creates a connector whose host resolves to two IPv6 and two IPv4 addresses.
	/// </summary>
	/// <param name="tcp">The fake connector the attempts go to.</param>
	/// <param name="attemptDelay">The wait before starting the next attempt.</param>
	/// <returns>The connector.</returns>
	private static HappyEyeballsConnector CreateConnector(FakeTcpConnector tcp, TimeSpan attemptDelay)
	{
		FakeHostResolver resolver = new FakeHostResolver
		(
			HappyEyeballsConnectorTests.Ipv4First,
			HappyEyeballsConnectorTests.Ipv4Second,
			HappyEyeballsConnectorTests.Ipv6First,
			HappyEyeballsConnectorTests.Ipv6Second
		);

		return new HappyEyeballsConnector(resolver, tcp, attemptDelay, TimeProvider.System);
	}

	/// <summary>
	/// Waits up to five seconds for a condition that abandoned attempts make true in the background.
	/// </summary>
	/// <param name="condition">The condition.</param>
	/// <returns>Whether it became true in time.</returns>
	private static async Task<bool> EventuallyAsync(Func<bool> condition)
	{
		DateTime deadline = DateTime.UtcNow.AddSeconds(5);

		while (!condition())
		{
			if (DateTime.UtcNow >= deadline)
			{
				return false;
			}

			await Task.Delay(20, TestContext.Current.CancellationToken);
		}

		return true;
	}
}
