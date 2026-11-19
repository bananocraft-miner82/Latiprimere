using Latiprimere.Cups;
using Latiprimere.Ipp;
using Latiprimere.Rendering.Pdfium;
using Latiprimere.Windows;

namespace Latiprimere.ApiReference;

/// <summary>
/// The packages the reference documents, in the order the index page lists them.
/// </summary>
internal static class ApiPackages
{
	/// <summary>
	/// Gets every package, from the abstractions every other one depends on to the optional renderer.
	/// </summary>
	public static IReadOnlyList<ApiPackage> All { get; } =
	[
		new ApiPackage
		{
			Name        = "Latiprimere.Abstractions",
			Assembly    = typeof(IPrinter).Assembly,
			Description = "The interfaces, records, enums and exceptions every package shares, in the `Latiprimere` namespace. It comes with the other packages, so you don't install it yourself."
		},
		new ApiPackage
		{
			Name        = "Latiprimere",
			Assembly    = typeof(PrinterCatalog).Assembly,
			Description = "The printer catalog, which brings the print systems together, and following a job's status."
		},
		new ApiPackage
		{
			Name        = "Latiprimere.Ipp",
			Assembly    = typeof(IppNetworkOptions).Assembly,
			Description = "Printing straight to network printers over IPP and IPPS, on every platform."
		},
		new ApiPackage
		{
			Name        = "Latiprimere.Cups",
			Assembly    = typeof(CupsOptions).Assembly,
			Description = "Printing through CUPS: the local scheduler on Linux and macOS, or a CUPS server."
		},
		new ApiPackage
		{
			Name        = "Latiprimere.Windows",
			Assembly    = typeof(WindowsOptions).Assembly,
			Description = "Printing through the Windows spooler and its drivers."
		},
		new ApiPackage
		{
			Name        = "Latiprimere.Rendering.Pdfium",
			Assembly    = typeof(PdfiumRasterizer).Assembly,
			Description = "Rendering PDFs with PDFium, for Windows drivers and for network printers that can't take PDF."
		},
	];
}
