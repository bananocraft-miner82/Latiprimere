using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Reads what a printer asks for at the address it is reached at (<see cref="PrinterUriRequirements"/>).
/// </summary>
internal static class PrinterUriRequirementsReader
{
	/// <summary>The default port of both <c>ipp</c> and <c>ipps</c> (RFC 8010, RFC 7472).</summary>
	private const int IppPort = 631;

	/// <summary>
	/// Finds the entry of <c>printer-uri-supported</c> for the address a printer is reached at, and reads the
	/// authentication beside it.
	/// </summary>
	/// <param name="attributes">The printer's attributes, with <c>printer-uri-supported</c> and
	/// <c>uri-authentication-supported</c>.</param>
	/// <param name="printerUri">The address the printer is reached at.</param>
	/// <returns>The requirements, or <see langword="null"/> if the printer doesn't say, or lists no entry for that
	/// address.</returns>
	/// <remarks>
	/// Entries are matched by scheme, port and path, not host: printers name themselves, by a <c>.local</c> name or
	/// <c>localhost</c>, rather than by the address they're reached at. The first match wins.
	/// </remarks>
	internal static PrinterUriRequirements? Read(PrinterDescriptionAttributes attributes, Uri printerUri)
	{
		ArgumentNullException.ThrowIfNull(attributes);
		ArgumentNullException.ThrowIfNull(printerUri);

		Uri[]? uris = IppValueReader.ReadReference(attributes.PrinterUriSupported);
		UriAuthentication[]? authentications = IppValueReader.ReadReference(attributes.UriAuthenticationSupported);

		if (uris is null || authentications is null || !printerUri.IsAbsoluteUri)
		{
			return null;
		}

		// The two lists are parallel; an entry without an authentication beside it says nothing
		int count = Math.Min(uris.Length, authentications.Length);
		int index = Enumerable.Range(0, count).FirstOrDefault(entry => PrinterUriRequirementsReader.IsSameAddress(uris[entry], printerUri), -1);

		if (index < 0 || string.IsNullOrEmpty(authentications[index].Value))
		{
			return null;
		}

		return new PrinterUriRequirements
		{
			Authentication    = authentications[index].Value.ToLowerInvariant(),
			SecureAlternative = PrinterUriRequirementsReader.FindSecureAlternative(uris, printerUri)
		};
	}

	/// <summary>
	/// Checks whether an entry of <c>printer-uri-supported</c> is the address a printer is reached at, but for its host.
	/// </summary>
	/// <param name="entry">The entry.</param>
	/// <param name="printerUri">The address used.</param>
	/// <returns>Whether the scheme, port and path match.</returns>
	private static bool IsSameAddress(Uri entry, Uri printerUri)
	{
		return entry.IsAbsoluteUri
			   && string.Equals(entry.Scheme, printerUri.Scheme, StringComparison.OrdinalIgnoreCase)
			   && PrinterUriRequirementsReader.GetPort(entry) == PrinterUriRequirementsReader.GetPort(printerUri)
			   && string.Equals(PrinterUriRequirementsReader.GetPath(entry), PrinterUriRequirementsReader.GetPath(printerUri), StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Finds an <c>ipps</c> address the printer lists, preferring one with the same path, rewritten to the host the
	/// printer is reached at, for a printer reached over plain IPP.
	/// </summary>
	/// <param name="uris">The printer's <c>printer-uri-supported</c>.</param>
	/// <param name="printerUri">The address used.</param>
	/// <returns>The address, or <see langword="null"/> if the printer is reached over IPPS already or lists none.</returns>
	private static Uri? FindSecureAlternative(Uri[] uris, Uri printerUri)
	{
		if (printerUri.Scheme != "ipp")
		{
			return null;
		}

		Uri[] secure = uris.Where(uri => uri.IsAbsoluteUri && uri.Scheme == "ipps").ToArray();
		Uri? chosen = secure.FirstOrDefault(uri => string.Equals(PrinterUriRequirementsReader.GetPath(uri), PrinterUriRequirementsReader.GetPath(printerUri), StringComparison.OrdinalIgnoreCase))
					  ?? secure.FirstOrDefault();

		if (chosen is null)
		{
			return null;
		}

		// The printer's own name for itself may not resolve here; the host it is reached at does
		UriBuilder builder = new UriBuilder(chosen)
		{
			Host = printerUri.Host,
			Port = PrinterUriRequirementsReader.GetPort(chosen)
		};

		return builder.Uri;
	}

	/// <summary>
	/// Gets a URI's port, with IPP's 631 for an <c>ipp</c> or <c>ipps</c> URI that leaves it out.
	/// </summary>
	/// <param name="uri">The URI.</param>
	/// <returns>The port.</returns>
	private static int GetPort(Uri uri)
	{
		return uri.Port >= 0 ? uri.Port : PrinterUriRequirementsReader.IppPort;
	}

	/// <summary>
	/// Gets a URI's path without a trailing slash, so <c>/ipp/print/</c> and <c>/ipp/print</c> match.
	/// </summary>
	/// <param name="uri">The URI.</param>
	/// <returns>The path.</returns>
	private static string GetPath(Uri uri)
	{
		return uri.AbsolutePath.TrimEnd('/');
	}
}
