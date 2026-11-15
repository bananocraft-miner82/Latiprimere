namespace Latiprimere.Cli;

/// <summary>
/// The process exit codes returned by the CLI.
/// </summary>
internal static class ExitCodes
{
	/// <summary>The command succeeded.</summary>
	internal const int Success = 0;

	/// <summary>A print operation failed.</summary>
	internal const int PrintFailed = 1;

	/// <summary>No print system is available.</summary>
	internal const int PrintingUnavailable = 2;

	/// <summary>The command line was not understood.</summary>
	internal const int InvalidArguments = 64;
}
