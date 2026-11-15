using Latiprimere.Ipp.Testing.Fakes;

namespace Latiprimere.Ipp.Testing.Builders;

/// <summary>
/// Builds <see cref="IppPrintContext"/> instances with a fake client and CUPS document formats by default.
/// </summary>
internal sealed class IppPrintContextBuilder
{
	private IIppClient client = new FakeIppClient();                         // The client requests go to
	private IppDocumentFormats documentFormats = IppDocumentFormats.Cups;    // The MIME types documents are sent as
	private TimeSpan busyRetryDuration = TimeSpan.Zero;                      // How long jobs turned away as busy are resent

	/// <summary>Sets how long to resend jobs turned away as busy, with millisecond waits between attempts.</summary>
	/// <param name="busyRetryDuration">How long to keep resending.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithBusyRetryDuration(TimeSpan busyRetryDuration)
	{
		this.busyRetryDuration = busyRetryDuration;

		return this;
	}

	/// <summary>Sets the client.</summary>
	/// <param name="client">The client.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithClient(IIppClient client)
	{
		this.client = client;

		return this;
	}

	/// <summary>Sets the document formats.</summary>
	/// <param name="documentFormats">The document formats.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithDocumentFormats(IppDocumentFormats documentFormats)
	{
		this.documentFormats = documentFormats;

		return this;
	}

	/// <summary>
	/// Builds the context, with "tester" as the requesting user, and waits of a few milliseconds between resends of
	/// busy jobs so tests stay fast.
	/// </summary>
	/// <returns>The context.</returns>
	public IppPrintContext Build()
	{
		return new IppPrintContext
		{
			Client              = this.client,
			RequestingUserName  = "tester",
			DocumentFormats     = this.documentFormats,
			BusyRetryDuration   = this.busyRetryDuration,
			BusyRetryFirstDelay = TimeSpan.FromMilliseconds(1),
			BusyRetryMaxDelay   = TimeSpan.FromMilliseconds(4)
		};
	}
}
