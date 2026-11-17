using SharpIpp.Protocol;
using SharpIpp.Protocol.Models;

namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// The printer description attributes a fake printer answers Get-Printer-Attributes with, taken from a captured
/// response and changed as a scenario needs.
/// </summary>
/// <remarks>
/// The printer replaces the attributes that depend on how it runs: <c>printer-uri-supported</c>,
/// <c>uri-security-supported</c>, <c>uri-authentication-supported</c>, <c>printer-state</c>,
/// <c>printer-state-reasons</c>, <c>printer-state-message</c>, <c>printer-is-accepting-jobs</c> and
/// <c>queued-job-count</c>.
/// </remarks>
public sealed class FakePrinterProfile
{
	/// <summary>
	/// The profile captured from the test container's <c>ippeveprinter</c>: PDF, JPEG and PWG raster, one-sided,
	/// monochrome, Letter by default, with a manual tray.
	/// </summary>
	public const string Ippeveprinter = "ippeveprinter";

	/// <summary>
	/// The profile captured from an HP ENVY 5640 inkjet: two-sided and colour, but no PDF.
	/// </summary>
	public const string HpEnvy5640 = "hp-envy-5640";

	private readonly List<List<IppAttribute>> entries; // One list per attribute, as IppAttributeLists.Group makes them
	private readonly Lock entriesLock = new Lock();    // Guards entries, which a running printer reads

	/// <summary>
	/// Initialises a new instance of the <see cref="FakePrinterProfile"/> class.
	/// </summary>
	/// <param name="name">The profile's name.</param>
	/// <param name="entries">Its attributes.</param>
	private FakePrinterProfile(string name, List<List<IppAttribute>> entries)
	{
		this.Name = name;
		this.entries = entries;
	}

	/// <summary>
	/// Gets the names of the built-in profiles.
	/// </summary>
	public static IReadOnlyList<string> BuiltInNames { get; } = [FakePrinterProfile.Ippeveprinter, FakePrinterProfile.HpEnvy5640];

	/// <summary>
	/// Gets the profile's name.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Loads the <see cref="Ippeveprinter"/> profile.
	/// </summary>
	/// <param name="cancellationToken">Cancels loading.</param>
	/// <returns>A new copy of the profile.</returns>
	public static Task<FakePrinterProfile> LoadIppeveprinterAsync(CancellationToken cancellationToken = default)
	{
		return FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.Ippeveprinter, cancellationToken);
	}

	/// <summary>
	/// Loads a built-in profile by name.
	/// </summary>
	/// <param name="name">One of <see cref="BuiltInNames"/>.</param>
	/// <param name="cancellationToken">Cancels loading.</param>
	/// <returns>A new copy of the profile.</returns>
	/// <exception cref="ArgumentException">There is no built-in profile with that name.</exception>
	public static async Task<FakePrinterProfile> LoadBuiltInAsync(string name, CancellationToken cancellationToken = default)
	{
		string builtInName = FakePrinterProfile.BuiltInNames.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
			?? throw new ArgumentException($"There is no built-in printer profile '{name}'; there are {string.Join(", ", FakePrinterProfile.BuiltInNames)}.", nameof(name));

		await using Stream stream = typeof(FakePrinterProfile).Assembly.GetManifestResourceStream($"Profiles.{builtInName}.ipp")
			?? throw new InvalidOperationException($"The {builtInName} profile is missing from the assembly.");

		return await FakePrinterProfile.LoadAsync(stream, builtInName, cancellationToken);
	}

	/// <summary>
	/// Loads a profile from a captured Get-Printer-Attributes response, such as the <c>.ipp</c> files in
	/// <c>tests/Latiprimere.Ipp.Tests/Data</c>.
	/// </summary>
	/// <param name="ippResponse">The response, in IPP's binary encoding.</param>
	/// <param name="name">A name for the profile.</param>
	/// <param name="cancellationToken">Cancels loading.</param>
	/// <returns>The profile.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="ippResponse"/> is <see langword="null"/>.</exception>
	public static async Task<FakePrinterProfile> LoadAsync(Stream ippResponse, string name, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(ippResponse);

		IIppResponseMessage response = await new IppProtocol().ReadIppResponseAsync(ippResponse, cancellationToken);
		List<IppAttribute> attributes = response.PrinterAttributes.SelectMany(group => group).ToList();

		return new FakePrinterProfile(name, IppAttributeLists.Group(attributes));
	}

	/// <summary>
	/// Gets an attribute's values as text, as <see cref="IppAttributeRecord.Values"/> shows them.
	/// </summary>
	/// <param name="name">The attribute's name.</param>
	/// <returns>The values, or an empty list if the profile doesn't have the attribute.</returns>
	public IReadOnlyList<string> GetValues(string name)
	{
		lock (this.entriesLock)
		{
			List<IppAttribute>? entry = this.entries.FirstOrDefault(candidate => candidate[0].Name == name);

			return entry is null ? [] : IppAttributeLists.Describe(entry).Values;
		}
	}

	/// <summary>
	/// Sets an attribute to text values, replacing it if the profile has it.
	/// </summary>
	/// <param name="name">The attribute's name, such as <c>sides-supported</c>.</param>
	/// <param name="tag">The value tag, such as <see cref="Tag.Keyword"/>.</param>
	/// <param name="values">The values.</param>
	/// <returns>This profile, so changes can be chained.</returns>
	public FakePrinterProfile Set(string name, Tag tag, params string[] values)
	{
		ArgumentNullException.ThrowIfNull(values);

		return this.Replace(name, values.Select(value => new IppAttribute(tag, name, value)).ToList());
	}

	/// <summary>
	/// Sets an attribute to integer or enum values, replacing it if the profile has it.
	/// </summary>
	/// <param name="name">The attribute's name, such as <c>print-quality-supported</c>.</param>
	/// <param name="tag">The value tag, such as <see cref="Tag.Enum"/>.</param>
	/// <param name="values">The values.</param>
	/// <returns>This profile, so changes can be chained.</returns>
	public FakePrinterProfile Set(string name, Tag tag, params int[] values)
	{
		ArgumentNullException.ThrowIfNull(values);

		return this.Replace(name, values.Select(value => new IppAttribute(tag, name, value)).ToList());
	}

	/// <summary>
	/// Sets an attribute to a range of integers, such as <c>copies-supported</c>, replacing it if the profile has it.
	/// </summary>
	/// <param name="name">The attribute's name.</param>
	/// <param name="lower">The range's lowest value.</param>
	/// <param name="upper">The range's highest value.</param>
	/// <returns>This profile, so changes can be chained.</returns>
	public FakePrinterProfile SetRange(string name, int lower, int upper)
	{
		return this.Replace(name, [new IppAttribute(Tag.RangeOfInteger, name, new SharpIpp.Protocol.Models.Range(lower, upper))]);
	}

	/// <summary>
	/// Removes an attribute.
	/// </summary>
	/// <param name="name">The attribute's name.</param>
	/// <returns>This profile, so changes can be chained.</returns>
	public FakePrinterProfile Remove(string name)
	{
		return this.Replace(name, []);
	}

	/// <summary>
	/// Copies the attributes, leaving out those named.
	/// </summary>
	/// <param name="excludedNames">The attributes to leave out.</param>
	/// <returns>The attributes, flat.</returns>
	internal List<IppAttribute> CopyAttributes(IReadOnlySet<string> excludedNames)
	{
		lock (this.entriesLock)
		{
			return this.entries
				.Where(entry => !excludedNames.Contains(entry[0].Name))
				.SelectMany(entry => entry)
				.ToList();
		}
	}

	/// <summary>
	/// Replaces an attribute's values, adding the attribute if it is new.
	/// </summary>
	/// <param name="name">The attribute's name.</param>
	/// <param name="values">The new values; none removes the attribute.</param>
	/// <returns>This profile.</returns>
	private FakePrinterProfile Replace(string name, List<IppAttribute> values)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		lock (this.entriesLock)
		{
			int index = this.entries.FindIndex(entry => entry[0].Name == name);

			if (index >= 0)
			{
				this.entries.RemoveAt(index);
			}

			if (values.Count > 0)
			{
				this.entries.Insert(index >= 0 ? index : this.entries.Count, values);
			}
		}

		return this;
	}
}
