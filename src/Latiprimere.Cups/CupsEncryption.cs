namespace Latiprimere.Cups;

/// <summary>
/// Enumerates when requests to a CUPS server travel over an encrypted (TLS) connection.
/// </summary>
/// <remarks>
/// Only TCP connections are encrypted. Requests through the local scheduler's Unix domain socket never leave the
/// machine, and CUPS doesn't ask for encryption on it.
/// </remarks>
public enum CupsEncryption
{
	/// <summary>
	/// Encrypted when <see cref="CupsOptions.Credentials"/> are set, so the password never travels in clear text;
	/// otherwise not. A standard CUPS server accepts a password from another machine only over an encrypted
	/// connection.
	/// </summary>
	Automatic = 0,

	/// <summary>
	/// Always encrypted, which also keeps documents private on the network.
	/// </summary>
	Always = 1,

	/// <summary>
	/// Never encrypted. Credentials, if set, travel in clear text, so use it only with a server that accepts them
	/// that way and only on a network you trust.
	/// </summary>
	Never = 2,
}
