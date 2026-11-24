using Latiprimere.Rendering;

namespace Latiprimere.Ipp.Tests.Raster;

/// <summary>
/// A document whose pages are four quadrants: black top left, red top right, green bottom left, white bottom right.
/// It honours a band's rotation, so tests can tell which way a page was turned and mirrored.
/// </summary>
internal sealed class QuadrantDocument : IRasterDocument
{
	/// <summary>The grey of black.</summary>
	internal const int Black = 0;

	/// <summary>The grey of pure red, with Latiprimere's luma weights.</summary>
	internal const int Red = 77;

	/// <summary>The grey of pure green: (150 × 255 + 128) / 256, rounded down.</summary>
	internal const int Green = 149;

	/// <summary>The grey of white.</summary>
	internal const int White = 255;

	private readonly PageSize[] pages;  // Each page's size

	/// <summary>
	/// Initialises a new instance of the <see cref="QuadrantDocument"/> class.
	/// </summary>
	/// <param name="pages">Each page's size.</param>
	internal QuadrantDocument(params PageSize[] pages)
	{
		this.pages = pages;
	}

	/// <summary>Gets every band rendered, in order.</summary>
	internal List<RasterBand> Bands { get; } = [];

	/// <summary>Gets the failure rendering throws, if any.</summary>
	internal Exception? RenderFailure { get; init; }

	/// <summary>Gets a value indicating whether the document has been disposed.</summary>
	internal bool IsDisposed { get; private set; }

	/// <inheritdoc/>
	public int PageCount
	{
		get
		{
			return this.pages.Length;
		}
	}

	/// <inheritdoc/>
	public PageSize GetPageSize(int pageIndex)
	{
		return this.pages[pageIndex];
	}

	/// <inheritdoc/>
	public void RenderBand(int pageIndex, RasterBand band, Span<byte> pixels, RasterRenderOptions? options = null)
	{
		if (this.RenderFailure is not null)
		{
			throw this.RenderFailure;
		}

		this.Bands.Add(band);

		// The unturned page's size in pixels
		bool quarter = band.Rotation is RasterRotation.Clockwise90 or RasterRotation.Counterclockwise90;
		int sourceWidth = quarter ? band.PageHeight : band.PageWidth;
		int sourceHeight = quarter ? band.PageWidth : band.PageHeight;

		for (int row = 0; row < band.Height; row++)
		{
			int y = band.Top + row;

			for (int x = 0; x < band.PageWidth; x++)
			{
				// Where this output pixel comes from on the unturned page
				(int sx, int sy) = band.Rotation switch
				{
					RasterRotation.Counterclockwise90 => (sourceWidth - 1 - y, x),
					RasterRotation.Clockwise90 => (y, sourceHeight - 1 - x),
					RasterRotation.Half => (sourceWidth - 1 - x, sourceHeight - 1 - y),
					_ => (x, y),
				};

				bool right = sx * 2 >= sourceWidth;
				bool bottom = sy * 2 >= sourceHeight;

				(byte r, byte g, byte b) = (right, bottom) switch
				{
					(false, false) => ((byte)0, (byte)0, (byte)0),
					(true, false) => ((byte)255, (byte)0, (byte)0),
					(false, true) => ((byte)0, (byte)255, (byte)0),
					_ => ((byte)255, (byte)255, (byte)255),
				};

				if (band.PixelFormat == RasterPixelFormat.Gray8)
				{
					pixels[(row * band.Stride) + x] = (byte)(((77 * r) + (150 * g) + (29 * b) + 128) >> 8);

					continue;
				}

				int offset = (row * band.Stride) + (x * band.BytesPerPixel);
				pixels[offset] = b;
				pixels[offset + 1] = g;
				pixels[offset + 2] = r;
				pixels[offset + 3] = 0;
			}
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.IsDisposed = true;
	}
}
