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
	/// The options used when a caller specifies none: one copy, and the printer's defaults for everything else.
	/// </summary>
	public static readonly PrintOptions Default = new PrintOptions();

	private int copies = PrintOptions.MinimumCopies; // Number of copies to print

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
	/// Gets a file to print to instead of the printer's device, or <see langword="null"/> to print normally.
	/// </summary>
	/// <remarks>
	/// Supported by the Windows spooler, where it is required for virtual printers such as Microsoft Print to PDF
	/// in applications without a user interface. Print systems that can't print to a file throw
	/// <see cref="NotSupportedException"/> when this is set, rather than ignoring it.
	/// </remarks>
	public string? OutputFile { get; init; }
}
