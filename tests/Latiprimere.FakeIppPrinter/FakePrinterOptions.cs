using System.Net;

namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// How a fake printer is set up when it starts. What it does while running is in <see cref="FakePrinterBehaviour"/>.
/// </summary>
public sealed class FakePrinterOptions
{
	/// <summary>
	/// Gets the printer's attributes. Defaults to <see cref="FakePrinterProfile.LoadIppeveprinterAsync"/>.
	/// </summary>
	public FakePrinterProfile? Profile { get; init; }

	/// <summary>
	/// Gets the loopback address to listen on.
	/// </summary>
	public FakePrinterAddress Address { get; init; } = FakePrinterAddress.IPv4Loopback;

	/// <summary>
	/// Gets the port to listen on, or 0 for any free port.
	/// </summary>
	public int Port { get; init; }

	/// <summary>
	/// Gets the host name in the printer's URI, such as <c>localhost</c>. Defaults to the address listened on.
	/// </summary>
	public string? UriHost { get; init; }

	/// <summary>
	/// Gets the printer's path. Defaults to <c>/ipp/print</c>, as IPP Everywhere printers use.
	/// </summary>
	public string Path { get; init; } = "/ipp/print";

	/// <summary>
	/// Gets a value indicating whether the printer only takes encrypted connections (<c>ipps</c>), with a self-signed
	/// certificate made when it starts.
	/// </summary>
	public bool UseTls { get; init; }

	/// <summary>
	/// Gets the user name and password the printer requires, with HTTP Basic authentication, or
	/// <see langword="null"/> for none.
	/// </summary>
	public NetworkCredential? RequiredCredentials { get; init; }

	/// <summary>
	/// Gets a value indicating whether <see cref="RequiredCredentials"/> are asked for only to print, with Print-Job
	/// and Validate-Job, as most printers that ask for a password do; anyone may ask what the printer takes. Defaults to
	/// <see langword="false"/>: every request needs them.
	/// </summary>
	public bool RequiresCredentialsForJobsOnly { get; init; }

	/// <summary>
	/// Gets an action called with each request once it is answered, for showing requests as they arrive.
	/// </summary>
	public Action<RecordedRequest>? RequestRecorded { get; init; }
}
