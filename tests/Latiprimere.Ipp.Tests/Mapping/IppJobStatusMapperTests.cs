using Latiprimere.Ipp.Mapping;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

public sealed class IppJobStatusMapperTests
{
	[Theory]
	[InlineData(JobState.Pending, PrintJobState.Pending)]
	[InlineData(JobState.PendingHeld, PrintJobState.Held)]
	[InlineData(JobState.Processing, PrintJobState.Processing)]
	[InlineData(JobState.ProcessingStopped, PrintJobState.Stopped)]
	[InlineData(JobState.Canceled, PrintJobState.Canceled)]
	[InlineData(JobState.Aborted, PrintJobState.Aborted)]
	[InlineData(JobState.Completed, PrintJobState.Completed)]
	public void GivenIppJobStateWhenToPrintJobStateThenMapsToMatchingState
	(
		JobState ippState,
		PrintJobState expectedState
	)
	{
		// Arrange — each IPP job-state value and its Latiprimere equivalent (provided by InlineData above)
		JobState? state = ippState;

		// Act
		PrintJobState mappedState = IppJobStatusMapper.ToPrintJobState(state);

		// Assert
		Assert.Equal(expectedState, mappedState);
	}

	[Fact]
	public void GivenMissingJobStateWhenToPrintJobStateThenReturnsUnknown()
	{
		// Arrange
		JobState? state = null;

		// Act
		PrintJobState mappedState = IppJobStatusMapper.ToPrintJobState(state);

		// Assert
		Assert.Equal(PrintJobState.Unknown, mappedState);
	}

	[Fact]
	public void GivenCancelledJobWithReasonsButNoMessageWhenToPrintJobStatusThenExplainsTheReason()
	{
		// Arrange — as CUPS reports a job its printer refused
		JobDescriptionAttributes attributes = new JobDescriptionAttributes
		{
			JobState        = new IppValue<JobState>(JobState.Canceled),
			JobStateReasons = new IppValue<JobStateReason[]>([new JobStateReason("job-canceled-at-device")])
		};

		// Act
		PrintJobStatus status = IppJobStatusMapper.ToPrintJobStatus(attributes, "7", DateTimeOffset.UtcNow);

		// Assert
		Assert.Equal(PrintJobState.Canceled, status.State);
		Assert.Equal(["job-canceled-at-device"], status.StateReasons);
		Assert.Equal("The job was cancelled at the printer.", status.StateMessage);
	}

	[Theory]
	[InlineData("Paper jam in tray 2")]
	[InlineData("   ")]
	public void GivenReasonsAndAMessageWhenToPrintJobStatusThenKeepsThePrintersOwnMessage(string message)
	{
		// Arrange — a message, or one that is only spaces and says nothing (provided by InlineData above)
		JobDescriptionAttributes attributes = new JobDescriptionAttributes
		{
			JobState        = new IppValue<JobState>(JobState.Aborted),
			JobStateReasons = new IppValue<JobStateReason[]>([new JobStateReason("aborted-by-system")]),
			JobStateMessage = new StringWithLanguage(message)
		};

		// Act
		PrintJobStatus status = IppJobStatusMapper.ToPrintJobStatus(attributes, "7", DateTimeOffset.UtcNow);

		// Assert
		string expected = string.IsNullOrWhiteSpace(message) ? "The print system stopped the job." : message;
		Assert.Equal(expected, status.StateMessage);
		Assert.Equal(["aborted-by-system"], status.StateReasons);
	}

	[Fact]
	public void GivenNoReasonsWhenToPrintJobStatusThenHasNoneAndNoMessage()
	{
		// Arrange
		JobDescriptionAttributes attributes = new JobDescriptionAttributes { JobState = new IppValue<JobState>(JobState.Completed) };

		// Act
		PrintJobStatus status = IppJobStatusMapper.ToPrintJobStatus(attributes, "7", DateTimeOffset.UtcNow);

		// Assert
		Assert.Empty(status.StateReasons);
		Assert.Null(status.StateMessage);
	}

	[Fact]
	public void GivenJobAttributesWhenToPrintJobStatusThenMapsStateMessageAndPages()
	{
		// Arrange
		JobDescriptionAttributes attributes = new JobDescriptionAttributes
		{
			JobState                = new IppValue<JobState>(JobState.ProcessingStopped),
			JobStateMessage         = new StringWithLanguage("Out of paper"),
			JobImpressionsCompleted = new IppValue<int>(2)
		};

		DateTimeOffset timestamp = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

		// Act
		PrintJobStatus status = IppJobStatusMapper.ToPrintJobStatus(attributes, "7", timestamp);

		// Assert
		Assert.Equal("7", status.JobId);
		Assert.Equal(PrintJobState.Stopped, status.State);
		Assert.Equal("Out of paper", status.StateMessage);
		Assert.Equal(2, status.PagesCompleted);
		Assert.Equal(timestamp, status.Timestamp);
	}

	[Fact]
	public void GivenNoAttributesWhenToPrintJobStatusThenReturnsUnknownStatus()
	{
		// Arrange
		JobDescriptionAttributes? attributes = null;

		// Act
		PrintJobStatus status = IppJobStatusMapper.ToPrintJobStatus(attributes, "7", DateTimeOffset.UnixEpoch);

		// Assert
		Assert.Equal(PrintJobState.Unknown, status.State);
		Assert.Null(status.StateMessage);
		Assert.Null(status.PagesCompleted);
	}

	[Fact]
	public void GivenJobStateIppDoesNotDefineWhenToPrintJobStateThenReturnsUnknown()
	{
		// Arrange — a value from a newer IPP version, or a printer that sends nonsense
		JobState state = (JobState)99;

		// Act
		PrintJobState mappedState = IppJobStatusMapper.ToPrintJobState(state);

		// Assert
		Assert.Equal(PrintJobState.Unknown, mappedState);
	}

	[Theory]
	[InlineData(false, PrintJobState.Completed)]
	[InlineData(true, PrintJobState.Canceled)]
	public void GivenJobNoLongerListedWhenStatusCreatedThenReportsItFinishedWithAMessageUnlessCancelled
	(
		bool isCancelRequested,
		PrintJobState expectedState
	)
	{
		// Arrange — whether Latiprimere cancelled the job, and the state expected (provided by InlineData above)
		DateTimeOffset timestamp = DateTimeOffset.UnixEpoch;

		// Act
		PrintJobStatus status = IppJobStatusMapper.ToNoLongerListedStatus("7", isCancelRequested, timestamp);

		// Assert — a cancelled job's state says it all; otherwise the message says how the state was worked out
		Assert.Equal("7", status.JobId);
		Assert.Equal(expectedState, status.State);
		Assert.Equal(isCancelRequested, status.StateMessage is null);
		Assert.True(status.IsTerminal);
		Assert.Equal(timestamp, status.Timestamp);
	}

	[Theory]
	[InlineData(false, PrintJobState.Aborted)]
	[InlineData(true, PrintJobState.Canceled)]
	public void GivenJobsPrinterGoneWhenStatusCreatedThenReportsItEndedWithAMessageUnlessCancelled
	(
		bool isCancelRequested,
		PrintJobState expectedState
	)
	{
		// Arrange — whether Latiprimere cancelled the job before its queue was deleted, and the state expected
		// (provided by InlineData above)
		DateTimeOffset timestamp = DateTimeOffset.UnixEpoch;

		// Act
		PrintJobStatus status = IppJobStatusMapper.ToPrinterGoneStatus("7", isCancelRequested, timestamp);

		// Assert — final either way, so following the job ends
		Assert.Equal("7", status.JobId);
		Assert.Equal(expectedState, status.State);
		Assert.Equal(isCancelRequested, status.StateMessage is null);
		Assert.True(status.IsTerminal);
		Assert.Equal(timestamp, status.Timestamp);
	}
}
