namespace Latiprimere;

/// <summary>
/// How carefully to print, trading speed and ink for detail, as IPP's <c>print-quality</c> has it.
/// </summary>
public enum PrintQuality
{
	/// <summary>Use the printer's default.</summary>
	PrinterDefault = 0,

	/// <summary>Fast, with the least ink or toner.</summary>
	Draft = 1,

	/// <summary>The printer's usual quality.</summary>
	Normal = 2,

	/// <summary>The most detail, more slowly.</summary>
	High = 3,
}
