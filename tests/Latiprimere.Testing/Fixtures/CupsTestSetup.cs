namespace Latiprimere.Testing.Fixtures;

/// <summary>
/// Which set of test queues the CUPS server named by <see cref="IntegrationEnvironment.CupsServerVariable"/> has.
/// </summary>
public enum CupsTestSetup
{
	/// <summary>
	/// The test container in <c>tools/virtual-printers</c>: CUPS-PDF (the default), Raw, Paused, Secure and
	/// Virtual-IPP.
	/// </summary>
	Container = 0,

	/// <summary>
	/// A Mac's own CUPS, set up by <c>tools/macos-test-printers</c>: Raw, Paused and Virtual-IPP only.
	/// </summary>
	Mac = 1
}
