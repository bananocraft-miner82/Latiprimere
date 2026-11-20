using System.Globalization;
using Latiprimere.Rendering;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// One page of a printed PDF: its size, and what rendering it shows.
/// </summary>
/// <param name="Size">The page's size, in points, as displayed.</param>
/// <param name="Ink">The share of the page that isn't white, from 0 to 1.</param>
/// <param name="Colour">The share of the page in colour rather than grey, from 0 to 1.</param>
public sealed record PdfPage(PageSize Size, double Ink, double Colour)
{
	/// <summary>
	/// Gets a value indicating whether the page is wider than it is tall.
	/// </summary>
	public bool IsLandscape
	{
		get
		{
			return this.Size.WidthPoints > this.Size.HeightPoints;
		}
	}

	/// <summary>
	/// Gets the page's width in millimetres.
	/// </summary>
	public double WidthMillimeters
	{
		get
		{
			return this.Size.WidthPoints / PageSize.PointsPerInch * 25.4;
		}
	}

	/// <summary>
	/// Gets the page's height in millimetres.
	/// </summary>
	public double HeightMillimeters
	{
		get
		{
			return this.Size.HeightPoints / PageSize.PointsPerInch * 25.4;
		}
	}

	/// <summary>
	/// Says whether the page is a paper size, either way round, within a tolerance.
	/// </summary>
	/// <param name="widthMillimeters">The paper's width.</param>
	/// <param name="heightMillimeters">The paper's height.</param>
	/// <param name="toleranceMillimeters">How far each side may be out, for rounding in drivers.</param>
	/// <returns><see langword="true"/> if it is.</returns>
	public bool IsPaper(double widthMillimeters, double heightMillimeters, double toleranceMillimeters = 2)
	{
		double shortSide = Math.Min(this.WidthMillimeters, this.HeightMillimeters);
		double longSide = Math.Max(this.WidthMillimeters, this.HeightMillimeters);

		return Math.Abs(shortSide - Math.Min(widthMillimeters, heightMillimeters)) <= toleranceMillimeters
			&& Math.Abs(longSide - Math.Max(widthMillimeters, heightMillimeters)) <= toleranceMillimeters;
	}

	/// <summary>
	/// Describes the page for notes: size in millimetres, orientation, ink and colour.
	/// </summary>
	/// <returns>The description.</returns>
	public override string ToString()
	{
		return string.Create
		(
			CultureInfo.InvariantCulture,
			$"{this.WidthMillimeters:0} x {this.HeightMillimeters:0} mm {(this.IsLandscape ? "landscape" : "portrait")}, ink {this.Ink:P1}, colour {this.Colour:P1}"
		);
	}
}
