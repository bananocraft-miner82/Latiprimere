namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// How a sample program ended.
/// </summary>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="Output">What it wrote to standard output and standard error, in the order it arrived.</param>
public sealed record SampleRun(int ExitCode, string Output);
