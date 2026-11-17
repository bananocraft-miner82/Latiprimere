namespace Latiprimere.TestHarness;

/// <summary>
/// The harness's process exit codes.
/// </summary>
internal static class ExitCodes
{
	/// <summary>
	/// Every scenario that ran passed.
	/// </summary>
	public const int Success = 0;

	/// <summary>
	/// At least one scenario failed.
	/// </summary>
	public const int ScenarioFailed = 1;

	/// <summary>
	/// The command line wasn't understood.
	/// </summary>
	public const int InvalidArguments = 2;

	/// <summary>
	/// The run was cancelled with Ctrl+C.
	/// </summary>
	public const int Cancelled = 130;
}
