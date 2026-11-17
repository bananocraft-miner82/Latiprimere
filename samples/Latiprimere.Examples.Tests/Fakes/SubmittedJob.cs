using System.Collections.Generic;
using Latiprimere;

namespace LatiprimereExamples.Tests.Fakes;

/// <summary>
/// A job a <see cref="FakePrinter"/> was sent: the document and how it was to be printed.
/// </summary>
internal sealed record SubmittedJob
{
	/// <summary>
	/// Gets the document's format.
	/// </summary>
	public required DocumentFormat Format { get; init; }

	/// <summary>
	/// Gets the print options the job was sent with.
	/// </summary>
	public required PrintOptions Options { get; init; }

	/// <summary>
	/// Gets the document's bytes, as read when the job was sent.
	/// </summary>
	public required IReadOnlyList<byte> Document { get; init; }
}
