using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Protects each IPP request: sends it over TLS when asked, and adds the user name and password to it.
/// </summary>
/// <remarks>
/// <para>
/// SharpIppNext sends <c>ipp</c> URIs over plain HTTP. CUPS also accepts TLS on its IPP port, but it can't ask for
/// it in a way .NET understands: it answers HTTP 426 and expects the connection to be upgraded in place, which .NET
/// doesn't do. So TLS is chosen up front, by rewriting the request URI from <c>http</c> to <c>https</c> on the same
/// port. The <c>printer-uri</c> inside the IPP request is unchanged.
/// </para>
/// <para>
/// Credentials are sent with every request, as HTTP Basic authentication, rather than in answer to a challenge.
/// CUPS checks a job's permissions only after receiving the whole document, so answering a challenge would mean
/// sending the document twice; and a document read from a stream that can't seek can't be sent twice at all.
/// </para>
/// </remarks>
internal sealed class IppConnectionHandler : DelegatingHandler
{
	private readonly bool useTls;                              // Whether to rewrite http to https
	private readonly AuthenticationHeaderValue? authorization; // The Basic credentials, or null for none

	/// <summary>
	/// Initialises a new instance of the <see cref="IppConnectionHandler"/> class.
	/// </summary>
	/// <param name="security">How to protect requests.</param>
	/// <param name="innerHandler">The handler that sends the requests.</param>
	/// <exception cref="ArgumentNullException"><paramref name="security"/> or <paramref name="innerHandler"/> is
	/// <see langword="null"/>.</exception>
	public IppConnectionHandler(IppConnectionSecurity security, HttpMessageHandler innerHandler)
		: base(innerHandler)
	{
		ArgumentNullException.ThrowIfNull(security);
		ArgumentNullException.ThrowIfNull(innerHandler);

		this.useTls = security.UseTls;
		this.authorization = IppConnectionHandler.CreateAuthorization(security.Credentials);
	}

	/// <summary>
	/// Protects the request, then sends it.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The response.</returns>
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (this.useTls && request.RequestUri is not null && request.RequestUri.Scheme == Uri.UriSchemeHttp)
		{
			request.RequestUri = IppConnectionHandler.ToHttps(request.RequestUri);
		}

		if (this.authorization is not null)
		{
			request.Headers.Authorization = this.authorization;
		}

		return base.SendAsync(request, cancellationToken);
	}

	/// <summary>
	/// Creates the Basic authorization header for a user name and password.
	/// </summary>
	/// <param name="credentials">The credentials, or <see langword="null"/>.</param>
	/// <returns>The header, or <see langword="null"/> when there are no credentials.</returns>
	private static AuthenticationHeaderValue? CreateAuthorization(NetworkCredential? credentials)
	{
		if (credentials is null)
		{
			return null;
		}

		// RFC 7617: user-id ":" password, in UTF-8, base64-encoded
		byte[] userPass = Encoding.UTF8.GetBytes(credentials.UserName + ":" + credentials.Password);

		return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(userPass));
	}

	/// <summary>
	/// Rewrites an <c>http</c> URI to <c>https</c>, keeping its port.
	/// </summary>
	/// <param name="uri">The URI.</param>
	/// <returns>The <c>https</c> URI.</returns>
	private static Uri ToHttps(Uri uri)
	{
		// An implied port 80 becomes an implied 443; SharpIppNext always names IPP's port 631 explicitly
		UriBuilder builder = new UriBuilder(uri)
		{
			Scheme = Uri.UriSchemeHttps,
			Port   = uri.IsDefaultPort ? -1 : uri.Port
		};

		return builder.Uri;
	}
}
