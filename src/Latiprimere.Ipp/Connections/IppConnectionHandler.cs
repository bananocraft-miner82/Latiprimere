using System.Net;
using System.Net.Http.Headers;
using Latiprimere.Ipp.Resources;

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
	private readonly IppConnectionSecurity security; // How to protect requests

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

		this.security = security;
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

		if (this.security.UseTls && request.RequestUri is not null && request.RequestUri.Scheme == Uri.UriSchemeHttp)
		{
			request.RequestUri = IppConnectionHandler.ToHttps(request.RequestUri);
		}

		// The last check before anything is sent: lookups already refuse plain addresses when encryption is required,
		// so this catches only a path that missed it (ADR 0058)
		if (this.security.RequiresTls && request.RequestUri?.Scheme != Uri.UriSchemeHttps)
		{
			throw new PrintException(ExceptionMessages.FormatIppPlainRequestRefused(request.RequestUri?.ToString() ?? string.Empty));
		}

		// The printer's own credentials, or those for all; for direct IPP, only over TLS (ADR 0037)
		NetworkCredential? credentials = request.RequestUri is null ? null : this.security.FindCredentialsToSend(request.RequestUri);
		AuthenticationHeaderValue? authorization = BasicAuthentication.CreateHeader(credentials);

		if (authorization is not null)
		{
			request.Headers.Authorization = authorization;
		}

		return base.SendAsync(request, cancellationToken);
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
