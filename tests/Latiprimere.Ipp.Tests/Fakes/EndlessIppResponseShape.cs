namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// What an <see cref="EndlessIppResponseStream"/> sends again and again.
/// </summary>
public enum EndlessIppResponseShape
{
	/// <summary>Another value of <c>printer-name</c>.</summary>
	MoreValues,

	/// <summary>Another attribute, each with the same name.</summary>
	MoreAttributes,

	/// <summary>Another printer attributes group, each holding an attribute.</summary>
	MoreGroups,
}
