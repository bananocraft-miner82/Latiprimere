using System.Net.Sockets;
using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppConnectionErrorsTests
{
	[Theory]
	[InlineData(HttpRequestError.ConnectionError)]
	[InlineData(HttpRequestError.NameResolutionError)]
	public void GivenConnectionThatNeverOpenedWhenIsConnectFailureThenReturnsTrue(HttpRequestError error)
	{
		// Arrange — refused, unreachable, or a name that didn't resolve (provided by InlineData above)
		PrintException exception = new PrintException("Could not communicate.", new HttpRequestException(error, "Failed."));

		// Act
		bool isConnectFailure = IppConnectionErrors.IsConnectFailure(exception);

		// Assert
		Assert.True(isConnectFailure);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void GivenStalledUploadWhenIsConnectFailureThenReturnsFalse(bool isAwaitingResponse)
	{
		// Arrange — some of the document may have reached the printer, so a Windows queue's job mustn't go through
		// the spooler as well (isAwaitingResponse provided by InlineData above)
		PrintException exception = new PrintException
		(
			"Stopped taking the document.",
			new Latiprimere.Ipp.Connections.IppTransferStalledException(isAwaitingResponse, TimeSpan.FromMinutes(5), new TaskCanceledException())
		);

		// Act
		bool isConnectFailure = IppConnectionErrors.IsConnectFailure(exception);

		// Assert
		Assert.False(isConnectFailure);
	}

	[Fact]
	public void GivenConnectTimeoutWhenIsConnectFailureThenReturnsTrue()
	{
		// Arrange — how SocketsHttpHandler reports its ConnectTimeout running out
		PrintException exception = new PrintException
		(
			"Did not answer in time.",
			new TaskCanceledException("Timed out.", new TimeoutException())
		);

		// Act
		bool isConnectFailure = IppConnectionErrors.IsConnectFailure(exception);

		// Assert
		Assert.True(isConnectFailure);
	}

	[Theory]
	[InlineData(HttpRequestError.SecureConnectionError)]
	[InlineData(HttpRequestError.ResponseEnded)]
	[InlineData(HttpRequestError.Unknown)]
	public void GivenFailureOnceConnectedWhenIsConnectFailureThenReturnsFalse(HttpRequestError error)
	{
		// Arrange — the printer may already have the job (provided by InlineData above)
		PrintException exception = new PrintException("Could not communicate.", new HttpRequestException(error, "Failed."));

		// Act
		bool isConnectFailure = IppConnectionErrors.IsConnectFailure(exception);

		// Assert
		Assert.False(isConnectFailure);
	}

	[Fact]
	public void GivenRejectionOrOtherFailureWhenIsConnectFailureThenReturnsFalse()
	{
		// Arrange — a printer's refusal, a cancellation without a timeout, and a socket error the handler didn't wrap
		PrintException[] exceptions =
		[
			new PrintException("Rejected.") { NativeErrorCode = 0x040B },
			new PrintException("Cancelled.", new OperationCanceledException()),
			new PrintException("Socket.", new SocketException()),
		];

		// Act
		bool[] results = exceptions.Select(IppConnectionErrors.IsConnectFailure).ToArray();

		// Assert
		Assert.All(results, Assert.False);
	}
}
