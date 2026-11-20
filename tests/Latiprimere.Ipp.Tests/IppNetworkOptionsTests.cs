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
	public void GivenNewOptionsWhenReadThenUploadStallTimeoutIsFiveMinutes()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		TimeSpan uploadStallTimeout = options.UploadStallTimeout;

		// Assert
		Assert.Equal(TimeSpan.FromMinutes(5), uploadStallTimeout);
		Assert.Equal(IppNetworkOptions.DefaultUploadStallTimeout, uploadStallTimeout);
	}

	[Fact]
	public void GivenInfiniteWhenUploadStallTimeoutSetThenKeepsIt()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		options.UploadStallTimeout = Timeout.InfiniteTimeSpan;

		// Assert
		Assert.Equal(Timeout.InfiniteTimeSpan, options.UploadStallTimeout);
	}

	[Fact]
	public void GivenPositiveTimeoutWhenUploadStallTimeoutSetThenKeepsIt()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		options.UploadStallTimeout = TimeSpan.FromSeconds(30);

		// Assert
		Assert.Equal(TimeSpan.FromSeconds(30), options.UploadStallTimeout);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-2)]
	public void GivenTimeoutNotPositiveWhenUploadStallTimeoutSetThenThrowsArgumentOutOfRangeException(int milliseconds)
	{
		// Arrange — zero, or negative other than infinite (provided by InlineData above)
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		Action set = () => options.UploadStallTimeout = TimeSpan.FromMilliseconds(milliseconds);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(set);
	}

	[Fact]
	public void GivenTimeoutLongerThanTimersAllowWhenUploadStallTimeoutSetThenThrowsArgumentOutOfRangeException()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		Action set = () => options.UploadStallTimeout = TimeSpan.FromDays(30);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(set);
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

	[Fact]
	public void GivenNewOptionsWhenReadThenRasterFormatsArePwgThenAppleThenPclm()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions();

		// Act
		IList<IppRasterFormat> formats = options.RasterFormats;

		// Assert
		Assert.Equal([IppRasterFormat.PwgRaster, IppRasterFormat.AppleRaster, IppRasterFormat.Pclm], formats);
	}
}
