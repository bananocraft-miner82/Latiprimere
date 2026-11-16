using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp;

/// <summary>
/// IPP attributes from <see cref="PrintOptions.VendorOptions"/>, to send with a Print-Job request beside the ones
/// SharpIppNext's typed request carries.
/// </summary>
internal sealed record IppVendorAttributes
{
	/// <summary>No vendor attributes.</summary>
	public static readonly IppVendorAttributes None = new IppVendorAttributes();

	/// <summary>Gets the attributes for the operation group, such as <c>ipp-attribute-fidelity</c>.</summary>
	public IReadOnlyList<IppAttribute> Operation { get; init; } = Array.Empty<IppAttribute>();

	/// <summary>Gets the attributes for the job group, such as <c>print-quality</c>.</summary>
	public IReadOnlyList<IppAttribute> Job { get; init; } = Array.Empty<IppAttribute>();

	/// <summary>Gets a value indicating whether there are no attributes to add.</summary>
	public bool IsEmpty
	{
		get
		{
			return this.Operation.Count == 0 && this.Job.Count == 0;
		}
	}
}
