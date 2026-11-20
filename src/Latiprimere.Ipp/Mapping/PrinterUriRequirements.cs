namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// What a printer says it asks for at the address it is reached at, from its <c>printer-uri-supported</c> and the
/// <c>uri-authentication-supported</c> beside it (RFC 8011, section 5.4.2).
/// </summary>
internal sealed record PrinterUriRequirements
{
	/// <summary>The keyword for HTTP Basic authentication, the only kind Latiprimere sends.</summary>
	internal const string Basic = "basic";

	/// <summary>
	/// Gets the authentication the printer asks for at the address used, as its keyword, such as <c>none</c>,
	/// <c>requesting-user-name</c> or <c>basic</c>.
	/// </summary>
	public required string Authentication { get; init; }

	/// <summary>
	/// Gets an encrypted (<c>ipps</c>) address the printer lists, at the host it is reached at, when it is reached
	/// over plain IPP; or <see langword="null"/>.
	/// </summary>
	public Uri? SecureAlternative { get; init; }

	/// <summary>
	/// Gets a value indicating whether the printer asks for a user name and password that Latiprimere can send:
	/// HTTP Basic.
	/// </summary>
	public bool AsksForPassword
	{
		get
		{
			return this.Authentication == PrinterUriRequirements.Basic;
		}
	}

	/// <summary>
	/// Gets a value indicating whether the printer asks for authentication Latiprimere can't give: <c>digest</c>,
	/// <c>certificate</c>, <c>negotiate</c> or anything else it doesn't know. <c>none</c> and
	/// <c>requesting-user-name</c>, which only names the user, need nothing.
	/// </summary>
	public bool AsksForUnsupportedAuthentication
	{
		get
		{
			return this.Authentication is not ("none" or "requesting-user-name" or PrinterUriRequirements.Basic);
		}
	}
}
