namespace Latiprimere.Ipp.Raster;

/// <summary>
/// The raster document formats Latiprimere writes, for printers that don't take PDF (ADR 0034, ADR 0046).
/// </summary>
internal enum RasterFormat
{
	/// <summary>PWG raster (<c>image/pwg-raster</c>, PWG 5102.4), which IPP Everywhere printers must take.</summary>
	PwgRaster = 0,

	/// <summary>Apple raster (<c>image/urf</c>), which AirPrint printers must take.</summary>
	AppleRaster = 1,

	/// <summary>PCLm (<c>application/PCLm</c>, Mopria): a restricted PDF holding each page as image strips.</summary>
	Pclm = 2,
}
