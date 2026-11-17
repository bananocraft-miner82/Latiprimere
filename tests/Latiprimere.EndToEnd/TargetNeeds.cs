namespace Latiprimere.EndToEnd;

/// <summary>
/// The test printers and other resources a scenario needs. A scenario runs only where all of them are available,
/// and is skipped elsewhere.
/// </summary>
[Flags]
public enum TargetNeeds
{
	/// <summary>
	/// Nothing beyond the machine itself.
	/// </summary>
	None = 0,

	/// <summary>
	/// A CUPS server with the test queues: the test container or a Mac's own CUPS
	/// (<c>LATIPRIMERE_TEST_CUPS_SERVER</c>).
	/// </summary>
	CupsServer = 1,

	/// <summary>
	/// The folder where that server's test queues save their output, readable from here
	/// (<c>LATIPRIMERE_TEST_CUPS_OUTPUT</c>).
	/// </summary>
	CupsOutput = 2,

	/// <summary>
	/// The test container's own queues, CUPS-PDF and Secure, which a Mac's CUPS doesn't have.
	/// </summary>
	CupsContainer = 4,

	/// <summary>
	/// A virtual IPP printer such as the container's <c>ippeveprinter</c> (<c>LATIPRIMERE_TEST_IPP_PRINTER</c>).
	/// </summary>
	IppPrinter = 8,

	/// <summary>
	/// The Windows spooler test printers from <c>tools/windows-test-printers</c>
	/// (<c>LATIPRIMERE_TEST_WINDOWS_OUTPUT</c>).
	/// </summary>
	WindowsTestPrinters = 16,

	/// <summary>
	/// Microsoft Print to PDF, which Windows installs by default.
	/// </summary>
	MicrosoftPrintToPdf = 32,

	/// <summary>
	/// A person at the keyboard to answer questions: the console harness's manual mode only.
	/// </summary>
	Person = 64,

	/// <summary>
	/// An IPP printer announcing itself over mDNS on this network, by the name in <c>LATIPRIMERE_TEST_MDNS_NAME</c>,
	/// such as the test container's <c>Virtual IPP Printer</c> when it runs on the host network.
	/// </summary>
	MdnsPrinter = 128
}
