using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppPrintJobTests
{
	private static readonly Uri PrinterUri = new Uri("ipp://localhost:631/printers/Office"); // Printer the job is on

	[Fact]
	public async Task GivenJobWhenGetStatusThenRequestsJobAndMapsResponse()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient
		{
			JobAttributes = new JobDescriptionAttributes
			{
				JobState                = new IppValue<JobState>(JobState.Processing),
				JobImpressionsCompleted = new IppValue<int>(1)
			}
		};

		IppPrintJob job = IppPrintJobTests.CreateJob(client, 9);

		// Act
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		GetJobAttributesRequest request = Assert.IsType<GetJobAttributesRequest>(client.LastGetJobAttributesRequest);
		Assert.Equal(9, request.OperationAttributes?.JobId?.Value);
		Assert.Equal(IppPrintJobTests.PrinterUri, request.OperationAttributes?.PrinterUri?.Value);
		Assert.Equal("9", status.JobId);
		Assert.Equal(PrintJobState.Processing, status.State);
		Assert.Equal(1, status.PagesCompleted);
	}

	[Fact]
	public async Task GivenJobWhenCancelThenSendsCancelJobForThatJob()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		IppPrintJob job = IppPrintJobTests.CreateJob(client, 9);

		// Act
		bool isCancelled = await job.CancelAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.True(isCancelled);
		CancelJobRequest request = Assert.IsType<CancelJobRequest>(client.LastCancelJobRequest);
		Assert.Equal(9, request.OperationAttributes?.JobId?.Value);
		Assert.Equal("tester", request.OperationAttributes?.RequestingUserName?.Value);
	}

	[Fact]
	public async Task GivenFinishedJobWhenCancelThenReturnsFalse()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient
		{
			FailureToThrow = new PrintException("Not possible.")
			{
				NativeErrorCode = (int)IppStatusCode.ClientErrorNotPossible
			}
		};

		IppPrintJob job = IppPrintJobTests.CreateJob(client, 9);

		// Act
		bool isCancelled = await job.CancelAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.False(isCancelled);
	}

	[Fact]
	public async Task GivenJobPrinterNoLongerListsWhenGetStatusThenReportsItCompleted()
	{
		// Arrange — printers with little job history answer not-found soon after a job finishes, while the printer
		// itself still answers
		FakeIppClient client = IppPrintJobTests.CreateClientWithPrinter();
		client.JobFailureToThrow = IppPrintJobTests.CreateNotFound();
		IppPrintJob job = IppPrintJobTests.CreateJob(client, 9);

		// Act
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.True(status.IsTerminal);
		Assert.Equal("9", status.JobId);
		Assert.Contains("no longer lists the job", status.StateMessage, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenQueueDeletedWithItsJobWhenGetStatusThenReportsItAbortedNotCompleted()
	{
		// Arrange — the queue answers not-found too, as after a CUPS queue is deleted, which cancels its jobs
		FakeIppClient client = new FakeIppClient { JobFailureToThrow = IppPrintJobTests.CreateNotFound() };
		IppPrintJob job = IppPrintJobTests.CreateJob(client, 9);

		// Act
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Aborted, status.State);
		Assert.True(status.IsTerminal);
		Assert.Contains("no longer exists", status.StateMessage, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenCancelledJobPrinterNoLongerListsWhenGetStatusThenReportsItCancelled()
	{
		// Arrange — the cancel succeeds, then the printer forgets the job
		FakeIppClient client = IppPrintJobTests.CreateClientWithPrinter();
		IppPrintJob job = IppPrintJobTests.CreateJob(client, 9);
		await job.CancelAsync(TestContext.Current.CancellationToken);
		client.JobFailureToThrow = IppPrintJobTests.CreateNotFound();

		// Act
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Cancelled, status.State);
	}

	[Fact]
	public async Task GivenJobPrinterNoLongerListsWhenCancelThenReturnsFalse()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient { JobFailureToThrow = IppPrintJobTests.CreateNotFound() };
		IppPrintJob job = IppPrintJobTests.CreateJob(client, 9);

		// Act
		bool isCancelled = await job.CancelAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.False(isCancelled);
	}

	[Fact]
	public async Task GivenOtherFailureWhenCancelThenPrintExceptionPropagates()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient
		{
			FailureToThrow = new PrintException("Forbidden.")
			{
				NativeErrorCode = (int)IppStatusCode.ClientErrorForbidden
			}
		};

		IppPrintJob job = IppPrintJobTests.CreateJob(client, 9);

		// Act
		Func<Task> cancel = () => job.CancelAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintException>(cancel);
	}

	/// <summary>
	/// Creates a fake client on which the test printer answers.
	/// </summary>
	/// <returns>The client.</returns>
	private static FakeIppClient CreateClientWithPrinter()
	{
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppPrintJobTests.PrinterUri, new PrinterAttributesBuilder().WithName("Office").Build());

		return client;
	}

	/// <summary>
	/// Creates the failure the client reports for a job the printer no longer lists.
	/// </summary>
	/// <returns>A print exception with the IPP not-found status.</returns>
	private static PrintException CreateNotFound()
	{
		return new PrintException("Not found.") { NativeErrorCode = (int)IppStatusCode.ClientErrorNotFound };
	}

	/// <summary>
	/// Creates a job that uses the fake client.
	/// </summary>
	/// <param name="client">The fake IPP client.</param>
	/// <param name="jobNumber">The IPP job-id.</param>
	/// <returns>The job.</returns>
	private static IppPrintJob CreateJob(FakeIppClient client, int jobNumber)
	{
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).Build();

		return new IppPrintJob(context, IppPrintJobTests.PrinterUri, "cups:Office", jobNumber);
	}
}
