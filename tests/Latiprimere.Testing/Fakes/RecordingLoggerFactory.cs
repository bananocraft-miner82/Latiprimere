using Microsoft.Extensions.Logging;

namespace Latiprimere.Testing.Fakes;

/// <summary>
/// A logger factory whose loggers all record into one <see cref="RecordingLogger"/>, and which remembers the
/// categories asked for, so tests can check that dependency injection hands it to a print system.
/// </summary>
public sealed class RecordingLoggerFactory : ILoggerFactory
{
	private readonly List<string> categories = []; // Categories asked for, in order

	/// <summary>Gets the logger every category gets.</summary>
	public RecordingLogger Logger { get; } = new RecordingLogger();

	/// <summary>Gets the categories asked for, in order.</summary>
	public IReadOnlyList<string> Categories
	{
		get
		{
			return this.categories;
		}
	}

	/// <summary>Providers aren't used.</summary>
	/// <param name="provider">The provider.</param>
	public void AddProvider(ILoggerProvider provider)
	{
	}

	/// <summary>Returns <see cref="Logger"/>, recording the category.</summary>
	/// <param name="categoryName">The category.</param>
	/// <returns>The logger.</returns>
	public ILogger CreateLogger(string categoryName)
	{
		this.categories.Add(categoryName);

		return this.Logger;
	}

	/// <summary>Nothing to release.</summary>
	public void Dispose()
	{
	}
}
