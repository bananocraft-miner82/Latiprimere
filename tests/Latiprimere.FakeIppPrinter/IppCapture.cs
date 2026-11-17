using System.Net.Http.Headers;
using SharpIpp.Protocol;
using SharpIpp.Protocol.Models;

namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// Saves a printer's Get-Printer-Attributes response, as the captures in <c>tests/Latiprimere.Ipp.Tests/Data</c> are
/// made: the raw <c>.ipp</c> response, which tests replay and the fake printer serves, and a <c>.txt</c> to read.
/// </summary>
/// <remarks>
/// Only reads the printer. The response often identifies the device and its network, so the result lists the values
/// to replace before a capture is shared (see the folder's README).
/// </remarks>
public static class IppCapture
{
	/// <summary>
	/// Attributes that often identify the printer, its owner or its network.
	/// </summary>
	private static readonly string[] IdentifyingAttributes =
	[
		"printer-uuid",
		"device-uuid",
		"printer-device-id",
		"printer-serial-number",
		"printer-name",
		"printer-info",
		"printer-location",
		"printer-geo-location",
		"printer-dns-sd-name",
		"printer-wifi-ssid",
		"printer-uri-supported",
		"printer-more-info",
		"printer-icons",
		"printer-supply-info-uri",
		"printer-organization",
		"printer-organizational-unit"
	];

	/// <summary>
	/// Asks a printer for all its attributes and saves the answer.
	/// </summary>
	/// <param name="printerUri">The printer, as an <c>ipp</c> or <c>ipps</c> URI.</param>
	/// <param name="directory">The folder to save into, created if needed.</param>
	/// <param name="fileName">The name for both files, without extension, such as <c>hp-envy-5640-printer-attributes</c>.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The files and the values to replace before sharing them.</returns>
	/// <exception cref="ArgumentException">The URI isn't <c>ipp</c> or <c>ipps</c>.</exception>
	/// <exception cref="HttpRequestException">The printer couldn't be reached or answered with an HTTP error.</exception>
	public static async Task<IppCaptureResult> CaptureAsync(Uri printerUri, string directory, string fileName, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(printerUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

		Uri httpUri = IppCapture.ToHttpUri(printerUri);
		IppProtocol protocol = new IppProtocol();

		// The same request as ipptool's get-printer-attributes.test
		IppRequestMessage request = new IppRequestMessage
		{
			Version = new IppVersion(1, 1),
			IppOperation = IppOperation.GetPrinterAttributes,
			RequestId = 1,
			OperationAttributes =
			{
				new IppAttribute(Tag.Charset, "attributes-charset", "utf-8"),
				new IppAttribute(Tag.NaturalLanguage, "attributes-natural-language", "en"),
				new IppAttribute(Tag.Uri, "printer-uri", printerUri.AbsoluteUri),
				new IppAttribute(Tag.NameWithoutLanguage, "requesting-user-name", "latiprimere"),
				new IppAttribute(Tag.Keyword, "requested-attributes", "all"),
				new IppAttribute(Tag.Keyword, "requested-attributes", "media-col-database")
			}
		};

		using MemoryStream requestBody = new MemoryStream();
		await protocol.WriteIppRequestAsync(request, requestBody, cancellationToken);

		using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
		using ByteArrayContent content = new ByteArrayContent(requestBody.ToArray());
		content.Headers.ContentType = new MediaTypeHeaderValue("application/ipp");
		using HttpResponseMessage response = await client.PostAsync(httpUri, content, cancellationToken);
		response.EnsureSuccessStatusCode();
		byte[] answer = await response.Content.ReadAsByteArrayAsync(cancellationToken);

		using MemoryStream answerStream = new MemoryStream(answer, writable: false);
		IIppResponseMessage parsed = await protocol.ReadIppResponseAsync(answerStream, cancellationToken);
		List<IppAttributeRecord> attributes = IppAttributeLists.DescribeAll(parsed.PrinterAttributes.SelectMany(group => group));

		Directory.CreateDirectory(directory);
		string ippPath = Path.Combine(directory, fileName + ".ipp");
		string textPath = Path.Combine(directory, fileName + ".txt");
		await File.WriteAllBytesAsync(ippPath, answer, cancellationToken);

		List<string> lines = [$"{printerUri.AbsoluteUri}: Get-Printer-Attributes", $"    status-code = {parsed.StatusCode}"];
		lines.AddRange(attributes.Select(attribute => $"        {attribute.Name} ({attribute.Tag}) = {string.Join(",", attribute.Values)}"));
		await File.WriteAllLinesAsync(textPath, lines, cancellationToken);

		List<string> identifying = attributes
			.Where(attribute => IppCapture.IdentifyingAttributes.Contains(attribute.Name))
			.Select(attribute => attribute.ToString())
			.ToList();

		return new IppCaptureResult(ippPath, textPath, attributes.Count, identifying);
	}

	/// <summary>
	/// Turns an IPP URI into the HTTP address it is sent to: <c>ipp</c> is HTTP and <c>ipps</c> is HTTPS, on port 631
	/// unless the URI names another.
	/// </summary>
	/// <param name="printerUri">The printer's URI.</param>
	/// <returns>The HTTP address.</returns>
	/// <exception cref="ArgumentException">The URI isn't <c>ipp</c> or <c>ipps</c>.</exception>
	private static Uri ToHttpUri(Uri printerUri)
	{
		string scheme = printerUri.Scheme switch
		{
			"ipp" => Uri.UriSchemeHttp,
			"ipps" => Uri.UriSchemeHttps,
			_ => throw new ArgumentException($"'{printerUri}' isn't an ipp or ipps URI.", nameof(printerUri))
		};

		UriBuilder builder = new UriBuilder(printerUri) { Scheme = scheme, Port = printerUri.IsDefaultPort ? 631 : printerUri.Port };

		return builder.Uri;
	}
}
