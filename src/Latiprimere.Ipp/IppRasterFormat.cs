namespace Latiprimere.Ipp;

/// <summary>
/// The raster formats Latiprimere can render a PDF into for a printer that can't take PDF, for
/// <see cref="IppNetworkOptions.RasterFormats"/>.
/// </summary>
public enum IppRasterFormat
{
	/// <summary>PWG raster (<c>image/pwg-raster</c>), which IPP Everywhere printers take.</summary>
	PwgRaster = 0,

	/// <summary>Apple raster (<c>image/urf</c>), which AirPrint printers take.</summary>
	AppleRaster = 1,

	/// <summary>PCLm (<c>application/PCLm</c>), which Mopria printers take: each page as image strips in a PDF.</summary>
	Pclm = 2,
}
