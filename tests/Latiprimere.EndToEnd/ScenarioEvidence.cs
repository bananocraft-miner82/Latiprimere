namespace Latiprimere.EndToEnd;

/// <summary>
/// A file a scenario kept to show what it compared, such as a printer's output or a list of printers.
/// </summary>
/// <param name="Path">The file's full path.</param>
/// <param name="Description">What the file is.</param>
public sealed record ScenarioEvidence(string Path, string Description);
