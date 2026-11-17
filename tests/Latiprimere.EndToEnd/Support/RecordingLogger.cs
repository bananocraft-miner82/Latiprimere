using Microsoft.Extensions.Logging;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// A logger that keeps what is logged, so a scenario can check what Latiprimere reported. Register it as
/// <c>ILogger&lt;T&gt;</c> for the class whose logging the scenario checks.
/// </summary>
/// <typeparam name="T">The class that logs.</typeparam>
public sealed class RecordingLogger<T> : ILogger<T>
{
	private readonly List<string> entries = new List<string>(); // "Level: message", in order
	private readonly Lock entriesLock = new Lock();             // Guards entries

	/// <summary>
	/// Gets every entry logged, as <c>Level: message</c>, with the exception's message after a dash.
	/// </summary>
	public IReadOnlyList<string> Entries
	{
		get
		{
			lock (this.entriesLock)
			{
				return this.entries.ToList();
			}
		}
	}

	/// <summary>
	/// Starts a scope, which this logger ignores.
	/// </summary>
	/// <typeparam name="TState">The scope's state.</typeparam>
	/// <param name="state">The state.</param>
	/// <returns><see langword="null"/>.</returns>
	public IDisposable? BeginScope<TState>(TState state)
		where TState : notnull
	{
		return null;
	}

	/// <summary>
	/// Says every level is logged.
	/// </summary>
	/// <param name="logLevel">The level.</param>
	/// <returns><see langword="true"/>.</returns>
	public bool IsEnabled(LogLevel logLevel)
	{
		return true;
	}

	/// <summary>
	/// Keeps an entry.
	/// </summary>
	/// <typeparam name="TState">The entry's state.</typeparam>
	/// <param name="logLevel">The level.</param>
	/// <param name="eventId">The event.</param>
	/// <param name="state">The state.</param>
	/// <param name="exception">The exception, or <see langword="null"/>.</param>
	/// <param name="formatter">Formats the message.</param>
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		ArgumentNullException.ThrowIfNull(formatter);

		string entry = $"{logLevel}: {formatter(state, exception)}" + (exception is null ? string.Empty : $" - {exception.Message}");

		lock (this.entriesLock)
		{
			this.entries.Add(entry);
		}
	}
}
