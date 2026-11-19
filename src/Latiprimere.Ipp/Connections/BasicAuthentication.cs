using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// HTTP Basic authentication (RFC 7617), as direct IPP and CUPS send credentials: checking them, and making the
/// header.
/// </summary>
internal static class BasicAuthentication
{
	/// <summary>
	/// Checks credentials can be sent with HTTP Basic authentication: they have a user name, without a colon.
	/// </summary>
	/// <param name="credentials">The credentials.</param>
	/// <param name="withoutUserNameMessage">The message when there is no user name, naming the caller's options.</param>
	/// <param name="userNameHasColonMessage">The message when the user name has a colon, naming the caller's
	/// options.</param>
	/// <exception cref="ArgumentException">They have no user name, or one containing a colon.</exception>
	internal static void ValidateCredentials(NetworkCredential credentials, string withoutUserNameMessage, string userNameHasColonMessage)
	{
		ArgumentNullException.ThrowIfNull(credentials);

		if (string.IsNullOrWhiteSpace(credentials.UserName))
		{
			throw new ArgumentException(withoutUserNameMessage, nameof(credentials));
		}

		// The colon separates the user name from the password, so it can't be in the user name
		if (credentials.UserName.Contains(':', StringComparison.Ordinal))
		{
			throw new ArgumentException(userNameHasColonMessage, nameof(credentials));
		}
	}

	/// <summary>
	/// Creates the Basic authorization header for a user name and password.
	/// </summary>
	/// <param name="credentials">The credentials, or <see langword="null"/>.</param>
	/// <returns>The header, or <see langword="null"/> when there are no credentials.</returns>
	internal static AuthenticationHeaderValue? CreateHeader(NetworkCredential? credentials)
	{
		if (credentials is null)
		{
			return null;
		}

		// RFC 7617: user-id ":" password, in UTF-8, base64-encoded
		byte[] userPass = Encoding.UTF8.GetBytes(credentials.UserName + ":" + credentials.Password);

		return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(userPass));
	}
}
