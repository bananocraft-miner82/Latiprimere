namespace Latiprimere.Ipp.Testing.Fakes;

/// <summary>
/// A clock that moves on by a fixed step each time it is read, so code that measures how long something took sees the
/// same times on every machine, however busy. Its timers, such as those behind <see cref="Task.Delay(TimeSpan, TimeProvider)"/>,
/// still run on real time.
/// </summary>
public sealed class SteppingTimeProvider : TimeProvider
{
	private readonly long stepTicks; // How far each reading moves the clock, in ticks
	private long timestamp;          // The last reading, in ticks

	/// <summary>
	/// Initializes a new instance of the <see cref="SteppingTimeProvider"/> class.
	/// </summary>
	/// <param name="step">How far each reading moves the clock on.</param>
	public SteppingTimeProvider(TimeSpan step)
	{
		this.stepTicks = step.Ticks;
	}

	/// <inheritdoc/>
	public override long TimestampFrequency
	{
		get
		{
			return TimeSpan.TicksPerSecond;
		}
	}

	/// <inheritdoc/>
	public override long GetTimestamp()
	{
		return Interlocked.Add(ref this.timestamp, this.stepTicks);
	}
}
