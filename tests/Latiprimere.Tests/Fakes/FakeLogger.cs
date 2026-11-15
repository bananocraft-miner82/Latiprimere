using Microsoft.Extensions.Logging;

namespace Latiprimere.Tests.Fakes;

/// <summary>
/// A logger that keeps every entry in memory, so tests can check what was logged.
/// </summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class FakeLogger<T> : ILogger<T>
{
	private readonly List<LoggedEntry> entries = new List<LoggedEntry>(); // Entries logged, in order

	/// <summary>Gets the entries logged, in order.</summary>
	public IReadOnlyList<LoggedEntry> Entries
	{
		get
		{
			return this.entries;
		}
	}

	/// <summary>Scopes are not recorded.</summary>
	/// <typeparam name="TState">The scope state type.</typeparam>
	/// <param name="state">The scope state.</param>
	/// <returns>Always <see langword="null"/>.</returns>
	public IDisposable? BeginScope<TState>(TState state)
		where TState : notnull
	{
		return null;
	}

	/// <summary>Every level is enabled.</summary>
	/// <param name="logLevel">The level.</param>
	/// <returns>Always <see langword="true"/>.</returns>
	public bool IsEnabled(LogLevel logLevel)
	{
		return true;
	}

	/// <summary>Records the entry.</summary>
	/// <typeparam name="TState">The state type.</typeparam>
	/// <param name="logLevel">The level.</param>
	/// <param name="eventId">The event ID.</param>
	/// <param name="state">The state.</param>
	/// <param name="exception">The exception, if any.</param>
	/// <param name="formatter">Formats the message.</param>
	public void Log<TState>
	(
		LogLevel                         logLevel,
		EventId                          eventId,
		TState                           state,
		Exception?                       exception,
		Func<TState, Exception?, string> formatter
	)
	{
		this.entries.Add(new LoggedEntry(logLevel, formatter(state, exception), exception));
	}
}
