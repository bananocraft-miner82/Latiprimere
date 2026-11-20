using Latiprimere.Ipp.Resources;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Reads a job's <c>job-state-reasons</c> (RFC 8011, section 5.3.8; PWG 5100.x), and says what the ones that explain
/// how a job ended, or why it waits, mean, for a printer that gives no <c>job-state-message</c> of its own.
/// </summary>
internal static class IppJobStateReasons
{
	/// <summary>
	/// The reasons Latiprimere explains, most telling first: the cause of a cancellation or failure before who
	/// cancelled it, and both before why a job waits. Only the first a job has is explained.
	/// </summary>
	private static readonly string[] Explained =
	[
		"unsupported-attributes-or-values",
		"conflicting-attributes",
		"unsupported-document-format",
		"document-format-error",
		"document-unprintable-error",
		"unsupported-compression",
		"compression-error",
		"document-password-error",
		"document-permission-error",
		"document-security-error",
		"document-access-error",
		"submission-interrupted",
		"account-authorization-failed",
		"account-closed",
		"account-info-needed",
		"account-limit-reached",
		"job-canceled-after-timeout",
		"job-canceled-at-device",
		"job-canceled-by-operator",
		"job-canceled-by-user",
		"aborted-by-system",
		"job-completed-with-errors",
		"job-completed-with-warnings",
		"job-held-for-authorization",
		"job-held-for-button-press",
		"job-held-for-release",
		"job-password-wait",
		"job-held-for-review",
		"job-hold-until-specified",
		"printer-stopped",
		"printer-stopped-partly"
	];

	/// <summary>
	/// Reads a job's reasons, as reported, without <c>none</c> and without repeats.
	/// </summary>
	/// <param name="attributes">The job's attributes, or <see langword="null"/>.</param>
	/// <returns>The keywords, in lower case, empty when there are none.</returns>
	internal static IReadOnlyList<string> Read(JobDescriptionAttributes? attributes)
	{
		JobStateReason[]? reasons = IppValueReader.ReadReference(attributes?.JobStateReasons);

		if (reasons is null)
		{
			return Array.Empty<string>();
		}

		return reasons
			.Select(reason => reason.Value?.Trim().ToLowerInvariant())
			.OfType<string>()
			.Where(keyword => keyword.Length > 0 && keyword != "none")
			.Distinct(StringComparer.Ordinal)
			.ToArray();
	}

	/// <summary>
	/// Says what the most telling of a job's reasons means.
	/// </summary>
	/// <param name="reasons">The job's reasons, from <see cref="Read"/>.</param>
	/// <returns>The explanation, or <see langword="null"/> when none of them is one Latiprimere explains.</returns>
	internal static string? Explain(IReadOnlyList<string> reasons)
	{
		ArgumentNullException.ThrowIfNull(reasons);

		string? explained = IppJobStateReasons.Explained.FirstOrDefault(keyword => reasons.Contains(keyword, StringComparer.Ordinal));

		return explained is null ? null : IppJobStateReasons.Describe(explained);
	}

	/// <summary>
	/// Gives the explanation of one of <see cref="Explained"/>, read when needed so it follows the current culture.
	/// </summary>
	/// <param name="keyword">The keyword.</param>
	/// <returns>The explanation.</returns>
	private static string? Describe(string keyword)
	{
		return keyword switch
		{
			"unsupported-attributes-or-values" => DisplayStrings.JobReasonUnsupportedOptions,
			"conflicting-attributes" => DisplayStrings.JobReasonUnsupportedOptions,
			"unsupported-document-format" => DisplayStrings.JobReasonUnsupportedFormat,
			"document-format-error" => DisplayStrings.JobReasonDocumentUnreadable,
			"document-unprintable-error" => DisplayStrings.JobReasonDocumentUnreadable,
			"unsupported-compression" => DisplayStrings.JobReasonCompression,
			"compression-error" => DisplayStrings.JobReasonCompression,
			"document-password-error" => DisplayStrings.JobReasonDocumentProtected,
			"document-permission-error" => DisplayStrings.JobReasonDocumentProtected,
			"document-security-error" => DisplayStrings.JobReasonDocumentProtected,
			"document-access-error" => DisplayStrings.JobReasonDocumentIncomplete,
			"submission-interrupted" => DisplayStrings.JobReasonDocumentIncomplete,
			"account-authorization-failed" => DisplayStrings.JobReasonAccounting,
			"account-closed" => DisplayStrings.JobReasonAccounting,
			"account-info-needed" => DisplayStrings.JobReasonAccounting,
			"account-limit-reached" => DisplayStrings.JobReasonAccounting,
			"job-canceled-after-timeout" => DisplayStrings.JobReasonCanceledAfterTimeout,
			"job-canceled-at-device" => DisplayStrings.JobReasonCanceledAtDevice,
			"job-canceled-by-operator" => DisplayStrings.JobReasonCanceledByOperator,
			"job-canceled-by-user" => DisplayStrings.JobReasonCanceledByUser,
			"aborted-by-system" => DisplayStrings.JobReasonAbortedBySystem,
			"job-completed-with-errors" => DisplayStrings.JobReasonCompletedWithErrors,
			"job-completed-with-warnings" => DisplayStrings.JobReasonCompletedWithWarnings,
			"job-held-for-authorization" => DisplayStrings.JobReasonHeldAtPrinter,
			"job-held-for-button-press" => DisplayStrings.JobReasonHeldAtPrinter,
			"job-held-for-release" => DisplayStrings.JobReasonHeldAtPrinter,
			"job-password-wait" => DisplayStrings.JobReasonHeldAtPrinter,
			"job-held-for-review" => DisplayStrings.JobReasonHeldForReview,
			"job-hold-until-specified" => DisplayStrings.JobReasonHeldUntil,
			"printer-stopped" => DisplayStrings.JobReasonPrinterStopped,
			"printer-stopped-partly" => DisplayStrings.JobReasonPrinterStopped,
			_ => null,
		};
	}
}
