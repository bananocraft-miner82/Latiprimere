namespace Latiprimere.EndToEnd;

/// <summary>
/// A person's answer to a question about what they can see.
/// </summary>
/// <param name="Reply">Yes, no, or not answered.</param>
/// <param name="Comment">What they saw instead, when they said no; otherwise <see langword="null"/>.</param>
public sealed record PersonAnswer(PersonReply Reply, string? Comment);
