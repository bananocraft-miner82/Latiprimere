namespace Latiprimere.Windows.Gdi;

/// <summary>
/// Limits used when rendering pages for the GDI print path. Platform-neutral, so layout code and its tests can use
/// them anywhere.
/// </summary>
internal static class GdiRenderSettings
{
	/// <summary>The largest band rendered at once. A 600 DPI A4 page needs about 139 MB in one piece.</summary>
	internal const long MaxBandBytes = 16L * 1024 * 1024;

	/// <summary>The render resolution used when no cap is given: render at whatever the device uses.</summary>
	internal const int UnlimitedRenderDpi = int.MaxValue;
}
