namespace Latiprimere.Windows.Tests.Fakes;

/// <summary>
/// A clock that moves only when a test moves it.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
	private DateTimeOffset now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero); // The current time

	/// <inheritdoc/>
	public override DateTimeOffset GetUtcNow()
	{
		return this.now;
	}

	/// <summary>
	/// Moves the clock on.
	/// </summary>
	/// <param name="time">How far.</param>
	internal void Advance(TimeSpan time)
	{
		this.now += time;
	}
}
