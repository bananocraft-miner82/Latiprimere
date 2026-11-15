using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppNetworkOptionsTests
{
	[Fact]
	public void GivenNewOptionsWhenReadThenBusyRetryDurationIsTwoMinutes()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		TimeSpan busyRetryDuration = options.BusyRetryDuration;

		// Assert
		Assert.Equal(TimeSpan.FromMinutes(2), busyRetryDuration);
	}

	[Fact]
	public void GivenZeroWhenBusyRetryDurationSetThenRetriesAreOff()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		options.BusyRetryDuration = TimeSpan.Zero;

		// Assert
		Assert.Equal(TimeSpan.Zero, options.BusyRetryDuration);
	}

	[Fact]
	public void GivenNewOptionsWhenReadThenConnectTimeoutIsTenSeconds()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		TimeSpan connectTimeout = options.ConnectTimeout;

		// Assert
		Assert.Equal(TimeSpan.FromSeconds(10), connectTimeout);
	}

	[Fact]
	public void GivenZeroWhenConnectTimeoutSetThenThrowsArgumentOutOfRangeException()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		Action set = () => options.ConnectTimeout = TimeSpan.Zero;

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(set);
	}

	[Fact]
	public void GivenPositiveTimeoutWhenConnectTimeoutSetThenKeepsIt()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		options.ConnectTimeout = TimeSpan.FromSeconds(3);

		// Assert
		Assert.Equal(TimeSpan.FromSeconds(3), options.ConnectTimeout);
	}

	[Fact]
	public void GivenNegativeDurationWhenBusyRetryDurationSetThenThrowsArgumentOutOfRangeException()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		Action set = () => options.BusyRetryDuration = TimeSpan.FromSeconds(-1);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(set);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(25)]
	public void GivenDiscoveryDurationOutOfRangeWhenSetThenThrowsArgumentOutOfRangeException(int days)
	{
		// Arrange — days is negative, or longer than Task.Delay can wait (provided by InlineData above); it used to
		// be accepted, and then failed every listing
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		Action set = () => options.DiscoveryDuration = TimeSpan.FromDays(days);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(set);
	}

	[Fact]
	public void GivenZeroWhenDiscoveryDurationSetThenKeepsIt()
	{
		// Arrange — zero listens not at all, which is allowed
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		options.DiscoveryDuration = TimeSpan.Zero;

		// Assert
		Assert.Equal(TimeSpan.Zero, options.DiscoveryDuration);
	}
}
