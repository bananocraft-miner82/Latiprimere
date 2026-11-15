namespace Latiprimere.Rendering;

/// <summary>
/// The size of a document page.
/// </summary>
/// <param name="WidthPoints">The width in points (1/72 inch).</param>
/// <param name="HeightPoints">The height in points (1/72 inch).</param>
public readonly record struct PageSize(double WidthPoints, double HeightPoints)
{
	/// <summary>
	/// The number of points in an inch.
	/// </summary>
	public static readonly double PointsPerInch = 72.0;

	/// <summary>
	/// Gets a value indicating whether the page is wider than it is tall.
	/// </summary>
	public bool IsLandscape
	{
		get
		{
			return this.WidthPoints > this.HeightPoints;
		}
	}
}
