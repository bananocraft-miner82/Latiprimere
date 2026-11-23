using System.Globalization;
using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// A run of pages to print, counted from 1, both ends included, as <see cref="PrintOptions.PageRanges"/> takes them.
/// One page is a range whose first and last are the same: <c>new PageRange(3, 3)</c>.
/// </summary>
public readonly record struct PageRange
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PageRange"/> struct.
	/// </summary>
	/// <param name="first">The first page, from 1.</param>
	/// <param name="last">The last page, at least <paramref name="first"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="first"/> is less than 1, or
	/// <paramref name="last"/> is less than <paramref name="first"/>.</exception>
	public PageRange(int first, int last)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(first, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(last, first);

		this.First = first;
		this.Last = last;
	}

	/// <summary>
	/// Gets the first page, from 1.
	/// </summary>
	public int First { get; }

	/// <summary>
	/// Gets the last page.
	/// </summary>
	public int Last { get; }

	/// <summary>
	/// Writes the range as people write it: <c>3</c>, or <c>2-5</c>.
	/// </summary>
	/// <returns>The range as text.</returns>
	public override string ToString()
	{
		return this.First == this.Last
			? this.First.ToString(CultureInfo.InvariantCulture)
			: string.Create(CultureInfo.InvariantCulture, $"{this.First}-{this.Last}");
	}

	/// <summary>
	/// Checks ranges are in order and don't overlap, as IPP printers require.
	/// </summary>
	/// <param name="ranges">The ranges.</param>
	/// <param name="parameterName">The parameter to name in the exception.</param>
	/// <exception cref="ArgumentException">A range starts at or before the end of the one before it, or one is
	/// <c>default</c>, which has no pages.</exception>
	internal static void Validate(IReadOnlyList<PageRange> ranges, string parameterName)
	{
		for (int index = 0; index < ranges.Count; index++)
		{
			bool isEmpty = ranges[index].First < 1;
			bool isOutOfOrder = index > 0 && ranges[index].First <= ranges[index - 1].Last;

			if (isEmpty || isOutOfOrder)
			{
				string list = string.Join(", ", ranges);

				throw new ArgumentException(ExceptionMessages.FormatPageRangesNotInOrder(list), parameterName);
			}
		}
	}
}
