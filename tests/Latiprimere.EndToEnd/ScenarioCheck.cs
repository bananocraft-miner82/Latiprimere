namespace Latiprimere.EndToEnd;

/// <summary>
/// One thing a scenario checked, and whether it held.
/// </summary>
/// <param name="Name">What was checked, as a statement that should be true, such as "the file holds the same
/// bytes".</param>
/// <param name="Passed">Whether it was true.</param>
/// <param name="Detail">What was found, for the report; mostly useful when the check failed.</param>
public sealed record ScenarioCheck(string Name, bool Passed, string? Detail);
