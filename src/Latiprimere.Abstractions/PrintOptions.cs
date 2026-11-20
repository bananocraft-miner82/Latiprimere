using System.Collections.ObjectModel;
using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// Describes how a print job should be printed.
/// </summary>
/// <remarks>
/// Settings left at their defaults use the printer's own defaults. Settings the printer does not support are
/// handled by the print system, which may ignore them or reject the job.
/// </remarks>
public sealed record PrintOptions
{
	/// <summary>
	/// The smallest number of copies a job can request.
	/// </summary>
	/// <remarks>
	/// Declared before <see cref="Default"/> because static fields are initialised in declaration order.
	/// </remarks>
	public static readonly int MinimumCopies = 1;

	/// <summary>
	/// The prefixes a <see cref="VendorOptions"/> key may start with, one for each print system that reads them.
	/// </summary>
	private static readonly string[] VendorOptionPrefixes = ["ipp:", "cups:", "windows:"];

	/// <summary>
	/// The empty vendor options every new instance starts with. Declared before <see cref="Default"/>, which uses it.
	/// </summary>
	private static readonly IReadOnlyDictionary<string, string> NoVendorOptions =
		new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

	/// <summary>
	/// The options used when a caller specifies none: one copy, and the printer's defaults for everything else.
	/// </summary>
	public static readonly PrintOptions Default = new PrintOptions();

	private int copies = PrintOptions.MinimumCopies;                                          // Number of copies to print
	private IReadOnlyDictionary<string, string> vendorOptions = PrintOptions.NoVendorOptions; // Settings for one print system

	/// <summary>
	/// Gets the name shown for the job in print queues, or <see langword="null"/> to let the print system choose.
	/// </summary>
	public string? JobName { get; init; }

	/// <summary>
	/// Gets the number of copies to print. Defaults to one.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The value is less than <see cref="MinimumCopies"/>.</exception>
	public int Copies
	{
		get
		{
			return this.copies;
		}

		init
		{
			// A job must produce at least one copy
			ArgumentOutOfRangeException.ThrowIfLessThan(value, PrintOptions.MinimumCopies, nameof(PrintOptions.Copies));
			this.copies = value;
		}
	}

	/// <summary>
	/// Gets whether to print on one or both sides of the paper.
	/// </summary>
	public DuplexMode Duplex { get; init; } = DuplexMode.PrinterDefault;

	/// <summary>
	/// Gets the orientation in which to print pages.
	/// </summary>
	public PageOrientation Orientation { get; init; } = PageOrientation.PrinterDefault;

	/// <summary>
	/// Gets the paper size to print on, or <see langword="null"/> to use the printer's default.
	/// </summary>
	/// <remarks>
	/// Use one of the printer's <see cref="PrinterCapabilities.MediaSizes"/>. Sizes are matched by
	/// <see cref="MediaSize.Name"/> only. The Windows spooler refuses a size the printer's driver doesn't list with
	/// <see cref="NotSupportedException"/>; IPP printers and CUPS decide for themselves, and may print on another size.
	/// </remarks>
	public MediaSize? MediaSize { get; init; }

	/// <summary>
	/// Gets whether to print in colour or monochrome.
	/// </summary>
	public ColorMode ColorMode { get; init; } = ColorMode.PrinterDefault;

	/// <summary>
	/// Gets how an image is sized on the paper: by default, shrunk to fit if it is larger, otherwise at its own size.
	/// </summary>
	/// <remarks>
	/// It applies to images, from <see cref="PrintContent.FromJpeg"/> and <see cref="PrintContent.FromPng"/>, and is
	/// ignored for other documents.
	/// </remarks>
	public ScalingMode Scaling { get; init; } = ScalingMode.PrinterDefault;

	/// <summary>
	/// Gets the paper source to print from, or <see langword="null"/> to use the printer's default.
	/// </summary>
	/// <remarks>
	/// Use one of the printer's <see cref="PrinterCapabilities.InputTrays"/>: tray IDs only mean something to the
	/// printer that reported them. Trays are matched by <see cref="InputTray.Id"/>. The Windows spooler refuses a tray
	/// the printer's driver doesn't list with <see cref="NotSupportedException"/>.
	/// </remarks>
	public InputTray? InputTray { get; init; }

	/// <summary>
	/// Gets settings for one print system only, which the other options don't cover, such as print quality,
	/// stapling or a driver's own settings. Empty by default.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each key starts with the print system that reads it, and each print system ignores the others' keys:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// <c>ipp:</c>, read by CUPS and by network printers: an IPP attribute, such as <c>ipp:print-quality</c> =
	/// <c>5</c>. Values separated by commas are several values. Integers, <c>true</c> and <c>false</c>, keywords
	/// and names are recognised; collections such as <c>media-col</c> are not.
	/// </description></item>
	/// <item><description>
	/// <c>cups:</c>, read by CUPS only, and sent the same way: a CUPS option, such as <c>cups:fit-to-page</c> =
	/// <c>true</c>.
	/// </description></item>
	/// <item><description>
	/// <c>windows:devmode</c>, read by the Windows spooler: a DEVMODE saved from the same printer's driver, encoded
	/// in base64, used for PDF jobs instead of the driver's defaults.
	/// </description></item>
	/// <item><description>
	/// <c>windows:print-ticket</c>, read by the Windows spooler: a Print Ticket in XML, which the printer's driver
	/// merges onto the driver's defaults, or onto <c>windows:devmode</c>, for PDF jobs. It reaches the driver's own
	/// features as well as the Print Schema's. Other <c>windows:</c> keys are refused with
	/// <see cref="NotSupportedException"/>.
	/// </description></item>
	/// </list>
	/// <para>
	/// These settings aren't portable between print systems, or often between printers: use the typed options
	/// where they exist. A key that sets something a typed option already sends, such as <c>ipp:copies</c> with
	/// <see cref="Copies"/>, is refused when the job is submitted. The dictionary is copied when set, and record
	/// equality compares it by reference.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">The value, or one of its values, is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">A key doesn't start with <c>ipp:</c>, <c>cups:</c> or <c>windows:</c>
	/// followed by a name.</exception>
	public IReadOnlyDictionary<string, string> VendorOptions
	{
		get
		{
			return this.vendorOptions;
		}

		init
		{
			ArgumentNullException.ThrowIfNull(value, nameof(PrintOptions.VendorOptions));
			Dictionary<string, string> copy = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (KeyValuePair<string, string> option in value)
			{
				// A mistyped prefix would otherwise be ignored by every print system without a word
				bool hasPrefix = PrintOptions.VendorOptionPrefixes.Any
				(
					prefix => option.Key.Length > prefix.Length && option.Key.StartsWith(prefix, StringComparison.Ordinal)
				);

				if (!hasPrefix)
				{
					string message = ExceptionMessages.FormatVendorOptionKeyInvalid(option.Key);

					throw new ArgumentException(message, nameof(PrintOptions.VendorOptions));
				}

				ArgumentNullException.ThrowIfNull(option.Value, nameof(PrintOptions.VendorOptions));
				copy.Add(option.Key, option.Value);
			}

			this.vendorOptions = new ReadOnlyDictionary<string, string>(copy);
		}
	}

	/// <summary>
	/// Gets a file to print to instead of the printer's device, or <see langword="null"/> to print normally.
	/// </summary>
	/// <remarks>
	/// Supported by the Windows spooler, where it is required for virtual printers such as Microsoft Print to PDF
	/// in applications without a user interface. Print systems that can't print to a file throw
	/// <see cref="NotSupportedException"/> when this is set, rather than ignoring it.
	/// </remarks>
	public string? OutputFile { get; init; }
}
