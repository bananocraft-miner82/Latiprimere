using System.Net;
using Latiprimere.Ipp.Connections;

namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// Resolves every host name to the same configured addresses, counting lookups.
/// </summary>
internal sealed class FakeHostResolver : IHostResolver
{
	private readonly IPAddress[] addresses; // Returned for every name

	/// <summary>
	/// Initialises a new instance of the <see cref="FakeHostResolver"/> class.
	/// </summary>
	/// <param name="addresses">The addresses every name resolves to.</param>
	public FakeHostResolver(params IPAddress[] addresses)
	{
		this.addresses = addresses;
	}

	/// <summary>Gets how many lookups were made.</summary>
	public int LookupCount { get; private set; }

	/// <inheritdoc/>
	public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
	{
		this.LookupCount++;

		return Task.FromResult(this.addresses);
	}
}
