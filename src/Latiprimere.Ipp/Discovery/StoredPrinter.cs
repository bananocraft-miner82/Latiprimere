namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// One printer in the address store.
/// </summary>
internal sealed record StoredPrinter
{
	/// <summary>Gets the name it announces, as in its <c>ipp:dnssd:</c> identifier.</summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>Gets its address, an <c>ipp</c> or <c>ipps</c> URI.</summary>
	public string PrinterUri { get; init; } = string.Empty;

	/// <summary>Gets its IPPS address when it announced both, or <see langword="null"/>.</summary>
	public string? SecureUri { get; init; }

	/// <summary>Gets the UUID it announced, which it must still report at the address, or <see langword="null"/>.</summary>
	public string? Uuid { get; init; }

	/// <summary>
	/// Gets when it was last seen announced, or <see langword="null"/> in a file written before this was kept. Added
	/// within format 1: earlier versions ignore it, as they do any property they don't know.
	/// </summary>
	public DateTimeOffset? LastSeen { get; init; }

	/// <summary>
	/// Gets <see langword="true"/> once it has been reached over IPPS with a trusted certificate, so it is never
	/// reached over plain IPP again (ADR 0058); otherwise <see langword="null"/>, which isn't written. Added within
	/// format 1, as <see cref="LastSeen"/> was.
	/// </summary>
	public bool? ReachedOverIpps { get; init; }

	/// <summary>Gets its make and model, or <see langword="null"/>.</summary>
	public string? MakeAndModel { get; init; }

	/// <summary>Gets its location, or <see langword="null"/>.</summary>
	public string? Location { get; init; }
}
