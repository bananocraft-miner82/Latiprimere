namespace Latiprimere;

/// <summary>
/// The settings of <see cref="PrintOptions"/>, as flags, for saying which of them a printer applies
/// (<see cref="IPrinter.AppliedOptions"/>).
/// </summary>
/// <remarks>
/// A value is added for each setting a later version adds to <see cref="PrintOptions"/>. A printer that doesn't list
/// one is refused a job that sets it to anything but its default, with <see cref="NotSupportedException"/>, rather
/// than printing as if it weren't set. Treat a value you don't know as a setting you don't know. The flags are a
/// <see langword="long"/>, so there is room for 64 settings.
/// </remarks>
[Flags]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028:Enum storage should be Int32", Justification = "One flag per PrintOptions setting, and settings are added after 1.0, when the underlying type can't change (ADR 0057).")]
public enum PrintOptionKinds : long
{
	/// <summary>No settings.</summary>
	None = 0,

	/// <summary><see cref="PrintOptions.JobName"/>.</summary>
	JobName = 1 << 0,

	/// <summary><see cref="PrintOptions.Copies"/>.</summary>
	Copies = 1 << 1,

	/// <summary><see cref="PrintOptions.Duplex"/>.</summary>
	Duplex = 1 << 2,

	/// <summary><see cref="PrintOptions.Orientation"/>.</summary>
	Orientation = 1 << 3,

	/// <summary><see cref="PrintOptions.MediaSize"/>.</summary>
	MediaSize = 1 << 4,

	/// <summary><see cref="PrintOptions.ColorMode"/>.</summary>
	ColorMode = 1 << 5,

	/// <summary><see cref="PrintOptions.Scaling"/>.</summary>
	Scaling = 1 << 6,

	/// <summary><see cref="PrintOptions.InputTray"/>.</summary>
	InputTray = 1 << 7,

	/// <summary><see cref="PrintOptions.VendorOptions"/>.</summary>
	VendorOptions = 1 << 8,

	/// <summary><see cref="PrintOptions.OutputFile"/>.</summary>
	OutputFile = 1 << 9,

	/// <summary><see cref="PrintOptions.PageRanges"/>.</summary>
	PageRanges = 1 << 10,

	/// <summary><see cref="PrintOptions.Collate"/>.</summary>
	Collate = 1 << 11,

	/// <summary><see cref="PrintOptions.Quality"/>.</summary>
	Quality = 1 << 12,

	/// <summary><see cref="PrintOptions.Finishings"/>.</summary>
	Finishings = 1 << 13,

	/// <summary><see cref="PrintOptions.OutputBin"/>.</summary>
	OutputBin = 1 << 14,

	/// <summary><see cref="PrintOptions.MediaType"/>.</summary>
	MediaType = 1 << 15,
}
