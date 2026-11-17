using Latiprimere.Rendering;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// What a printed PDF holds, as far as the scenarios check it.
/// </summary>
/// <param name="Pages">Each page, in order.</param>
public sealed record PdfSummary(IReadOnlyList<PdfPage> Pages)
{
	/// <summary>
	/// Gets the number of pages.
	/// </summary>
	public int PageCount
	{
		get
		{
			return this.Pages.Count;
		}
	}

	/// <summary>
	/// Gets each page's size, in order.
	/// </summary>
	public IReadOnlyList<PageSize> PageSizes
	{
		get
		{
			return this.Pages.Select(page => page.Size).ToList();
		}
	}

	/// <summary>
	/// Describes every page, one line each, for evidence.
	/// </summary>
	/// <returns>The lines.</returns>
	public IReadOnlyList<string> Describe()
	{
		return this.Pages.Select((page, index) => $"Page {index + 1}: {page}").ToList();
	}
}
