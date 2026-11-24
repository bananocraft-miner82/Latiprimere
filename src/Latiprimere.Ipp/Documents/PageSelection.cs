using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Turns <see cref="PrintOptions.PageRanges"/> into the pages to render of a document whose page count is known
/// (ADR 0056).
/// </summary>
internal static class PageSelection
{
	/// <summary>
	/// Lists the pages to render, in order.
	/// </summary>
	/// <param name="ranges">The page ranges, counted from 1, in order; empty for every page.</param>
	/// <param name="pageCount">The document's pages.</param>
	/// <returns>The zero-based indices of the pages to render. Pages past the end are left out.</returns>
	/// <exception cref="ArgumentException">The ranges leave no pages of the document.</exception>
	internal static IReadOnlyList<int> Select(IReadOnlyList<PageRange> ranges, int pageCount)
	{
		ArgumentNullException.ThrowIfNull(ranges);

		if (ranges.Count == 0)
		{
			return Enumerable.Range(0, pageCount).ToArray();
		}

		List<int> pages = new List<int>();

		foreach (PageRange range in ranges)
		{
			for (int page = range.First; page <= Math.Min(range.Last, pageCount); page++)
			{
				pages.Add(page - 1);
			}
		}

		if (pages.Count == 0)
		{
			string message = ExceptionMessages.FormatPageRangesSelectNoPages(string.Join(", ", ranges), pageCount);

			throw new ArgumentException(message, nameof(ranges));
		}

		return pages;
	}
}
