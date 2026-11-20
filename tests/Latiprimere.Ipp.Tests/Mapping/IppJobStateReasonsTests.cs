using Latiprimere.Ipp.Mapping;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

public sealed class IppJobStateReasonsTests
{
	[Fact]
	public void GivenReasonsWithNoneRepeatsAndCaseWhenReadThenKeepsEachOnceInOrderInLowerCase()
	{
		// Arrange
		JobDescriptionAttributes attributes = IppJobStateReasonsTests.WithReasons("none", "Job-Canceled-At-Device", "processing-to-stop-point", "job-canceled-at-device", " ");

		// Act
		IReadOnlyList<string> reasons = IppJobStateReasons.Read(attributes);

		// Assert
		Assert.Equal(["job-canceled-at-device", "processing-to-stop-point"], reasons);
	}

	[Fact]
	public void GivenNoAttributesWhenReadThenReturnsNone()
	{
		// Act
		IReadOnlyList<string> reasons = IppJobStateReasons.Read(null);

		// Assert
		Assert.Empty(reasons);
	}

	[Fact]
	public void GivenCauseAndWhoCancelledWhenExplainThenExplainsTheCause()
	{
		// Arrange — the options the printer refused say more than that it cancelled the job
		string[] reasons = ["job-canceled-at-device", "unsupported-attributes-or-values"];

		// Act
		string? message = IppJobStateReasons.Explain(reasons);

		// Assert
		Assert.Equal("The printer can't print the job with the options chosen.", message);
	}

	[Theory]
	[InlineData("job-printing")]
	[InlineData("job-completed-successfully")]
	[InlineData("printer-vendor-reason")]
	public void GivenOnlyReasonsThatNeedNoExplainingWhenExplainThenReturnsNull(string keyword)
	{
		// Arrange — keyword provided by InlineData above
		string[] reasons = [keyword];

		// Act
		string? message = IppJobStateReasons.Explain(reasons);

		// Assert
		Assert.Null(message);
	}

	[Theory]
	[InlineData("unsupported-attributes-or-values")]
	[InlineData("conflicting-attributes")]
	[InlineData("unsupported-document-format")]
	[InlineData("document-format-error")]
	[InlineData("document-unprintable-error")]
	[InlineData("unsupported-compression")]
	[InlineData("compression-error")]
	[InlineData("document-password-error")]
	[InlineData("document-permission-error")]
	[InlineData("document-security-error")]
	[InlineData("document-access-error")]
	[InlineData("submission-interrupted")]
	[InlineData("account-authorization-failed")]
	[InlineData("account-closed")]
	[InlineData("account-info-needed")]
	[InlineData("account-limit-reached")]
	[InlineData("job-canceled-after-timeout")]
	[InlineData("job-canceled-at-device")]
	[InlineData("job-canceled-by-operator")]
	[InlineData("job-canceled-by-user")]
	[InlineData("aborted-by-system")]
	[InlineData("job-completed-with-errors")]
	[InlineData("job-completed-with-warnings")]
	[InlineData("job-held-for-authorization")]
	[InlineData("job-held-for-button-press")]
	[InlineData("job-held-for-release")]
	[InlineData("job-password-wait")]
	[InlineData("job-held-for-review")]
	[InlineData("job-hold-until-specified")]
	[InlineData("printer-stopped")]
	[InlineData("printer-stopped-partly")]
	public void GivenEachExplainedReasonWhenExplainThenGivesASentence(string keyword)
	{
		// Arrange — keyword provided by InlineData above
		string[] reasons = [keyword];

		// Act
		string? message = IppJobStateReasons.Explain(reasons);

		// Assert
		Assert.NotNull(message);
		Assert.EndsWith(".", message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Creates job attributes with the given <c>job-state-reasons</c>.
	/// </summary>
	/// <param name="keywords">The keywords.</param>
	/// <returns>The attributes.</returns>
	private static JobDescriptionAttributes WithReasons(params string[] keywords)
	{
		return new JobDescriptionAttributes
		{
			JobStateReasons = new IppValue<JobStateReason[]>(keywords.Select(keyword => new JobStateReason(keyword)).ToArray())
		};
	}
}
