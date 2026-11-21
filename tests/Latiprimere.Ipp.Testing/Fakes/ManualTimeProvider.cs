namespace Latiprimere.Ipp.Testing.Fakes;

/// <summary>
/// A clock whose time moves only when a test moves it, for code that compares wall-clock times days apart.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
	private DateTimeOffset now; // The time it gives

	/// <summary>
	/// Initialises a new instance of the <see cref="ManualTimeProvider"/> class, starting at noon UTC on 18 November
	/// 2026.
	/// </summary>
	public ManualTimeProvider()
		: this(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero))
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="ManualTimeProvider"/> class.
	/// </summary>
	/// <param name="start">The time it starts at.</param>
	public ManualTimeProvider(DateTimeOffset start)
	{
		this.now = start;
	}

	/// <inheritdoc/>
	public override DateTimeOffset GetUtcNow()
	{
		return this.now;
	}

	/// <summary>
	/// Moves the time on.
	/// </summary>
	/// <param name="time">How far.</param>
	public void Advance(TimeSpan time)
	{
		this.now += time;
	}
}
