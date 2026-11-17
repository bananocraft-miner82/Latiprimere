namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// Which address the fake printer listens on.
/// </summary>
public enum FakePrinterAddress
{
	/// <summary>
	/// <c>127.0.0.1</c> only.
	/// </summary>
	IPv4Loopback = 0,

	/// <summary>
	/// <c>::1</c> only.
	/// </summary>
	IPv6Loopback = 1,

	/// <summary>
	/// Every IPv4 address of this machine, so other machines and containers can reach it. For trying the printer by
	/// hand only: anyone on the network can then print to it.
	/// </summary>
	AnyIPv4 = 2
}
