using System.Net;
using Latiprimere.FakeIppPrinter;

namespace Latiprimere.TestHarness.Commands;

/// <summary>
/// The <c>fake-printer</c> command's options.
/// </summary>
internal sealed class FakePrinterSettings
{
	/// <summary>
	/// Gets or sets the port.
	/// </summary>
	public int Port { get; set; }

	/// <summary>
	/// Gets or sets the built-in profile's name.
	/// </summary>
	public string ProfileName { get; set; } = FakePrinterProfile.Ippeveprinter;

	/// <summary>
	/// Gets or sets a value indicating whether the printer takes ipps only.
	/// </summary>
	public bool UseTls { get; set; }

	/// <summary>
	/// Gets or sets the credentials it requires, or <see langword="null"/>.
	/// </summary>
	public NetworkCredential? Credentials { get; set; }

	/// <summary>
	/// Gets or sets the address it listens on.
	/// </summary>
	public FakePrinterAddress Address { get; set; } = FakePrinterAddress.IPv4Loopback;

	/// <summary>
	/// Gets or sets how many jobs it turns away as busy.
	/// </summary>
	public int BusyPrintJobs { get; set; }

	/// <summary>
	/// Gets or sets the wait before every answer.
	/// </summary>
	public TimeSpan ResponseDelay { get; set; }

	/// <summary>
	/// Gets or sets how long jobs stay processing.
	/// </summary>
	public TimeSpan ProcessingTime { get; set; }

	/// <summary>
	/// Gets or sets the reason it is stopped, or <see langword="null"/> when it isn't.
	/// </summary>
	public string? StoppedReason { get; set; }

	/// <summary>
	/// Gets or sets the message jobs abort with, or <see langword="null"/> when they complete.
	/// </summary>
	public string? AbortMessage { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether it drops the connection on every Print-Job.
	/// </summary>
	public bool DropConnection { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether jobs are checked against the profile.
	/// </summary>
	public bool ValidateJobs { get; set; } = true;

	/// <summary>
	/// Applies the behaviour options to a running printer.
	/// </summary>
	/// <param name="behaviour">The printer's behaviour.</param>
	public void ApplyTo(FakePrinterBehaviour behaviour)
	{
		behaviour.BusyPrintJobs = this.BusyPrintJobs;
		behaviour.ResponseDelay = this.ResponseDelay;
		behaviour.ProcessingTime = this.ProcessingTime;
		behaviour.DropConnectionOnPrintJob = this.DropConnection;
		behaviour.ValidateJobs = this.ValidateJobs;

		if (this.StoppedReason is not null)
		{
			behaviour.IsStopped = true;
			behaviour.StoppedReason = this.StoppedReason;
		}

		if (this.AbortMessage is not null)
		{
			behaviour.JobOutcome = FakeJobOutcome.Aborted;
			behaviour.JobOutcomeMessage = this.AbortMessage;
		}
	}
}
