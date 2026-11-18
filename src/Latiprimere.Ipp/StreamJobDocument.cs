using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;

namespace Latiprimere.Ipp;

/// <summary>
/// The caller's document, sent as it is. Only a stream that can seek is sent again, rewound to where it started.
/// </summary>
internal sealed class StreamJobDocument : IppJobDocument
{
	private readonly Stream content; // The document
	private readonly long? start;    // Where it started, or null if it can't seek
	private bool isSent;             // Whether an attempt has read from it

	/// <summary>
	/// Initialises a new instance of the <see cref="StreamJobDocument"/> class.
	/// </summary>
	/// <param name="content">The document, from its current position.</param>
	public StreamJobDocument(Stream content)
	{
		ArgumentNullException.ThrowIfNull(content);

		this.content = content;
		this.start = content.CanSeek ? content.Position : null;
	}

	/// <inheritdoc/>
	public override bool CanResend
	{
		get
		{
			return this.start is not null;
		}
	}

	/// <inheritdoc/>
	public override Task<PrintJobResponse> SendAsync
	(
		IIppClient          client,
		PrintJobRequest     request,
		IppVendorAttributes vendorAttributes,
		CancellationToken   cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(request);

		// An earlier attempt read some or all of it
		if (this.isSent && this.start is not null)
		{
			this.content.Position = this.start.Value;
		}

		this.isSent = true;
		request.Document = this.content;

		return client.PrintJobAsync(request, vendorAttributes, cancellationToken);
	}
}
