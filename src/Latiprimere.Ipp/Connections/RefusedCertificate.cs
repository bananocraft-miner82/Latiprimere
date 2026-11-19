namespace Latiprimere.Ipp.Connections;

/// <summary>
/// A printer certificate that <see cref="IppCertificateTrust"/> refused, so the error can say what to pin.
/// </summary>
internal sealed record RefusedCertificate
{
	/// <summary>Gets its SHA-256 fingerprint, as colon-separated pairs of hexadecimal digits.</summary>
	public required string Fingerprint { get; init; }

	/// <summary>
	/// Gets a value indicating whether the system trusts it, so it was refused only because the shared credentials
	/// go no further than certificates the application vouched for (ADR 0037).
	/// </summary>
	public bool IsTrustedBySystem { get; init; }
}
