using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// Finds what a virtual printer saved for a job that isn't known by ID, such as one a sample program sent.
/// </summary>
public static class OutputSearch
{
	/// <summary>
	/// Waits for a file in a folder, written since a moment, that contains some bytes.
	/// </summary>
	/// <param name="directory">The printer's output folder.</param>
	/// <param name="since">Files written earlier are ignored.</param>
	/// <param name="expected">Bytes the file must contain.</param>
	/// <param name="cancellationToken">Cancels the wait.</param>
	/// <returns>The file's path, or <see langword="null"/> if none appeared within
	/// <see cref="OutputFileWaiter.Timeout"/>.</returns>
	public static async Task<string?> FindNewFileContainingAsync(string directory, DateTime since, byte[] expected, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentNullException.ThrowIfNull(expected);

		DateTime deadline = DateTime.UtcNow + OutputFileWaiter.Timeout;

		while (true)
		{
			string? match = OutputSearch.FindNewFileContaining(directory, since, expected);

			if (match is not null || DateTime.UtcNow >= deadline)
			{
				return match;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
		}
	}

	/// <summary>
	/// Looks once for a file written since a moment that contains some bytes.
	/// </summary>
	/// <param name="directory">The folder.</param>
	/// <param name="since">Files written earlier are ignored.</param>
	/// <param name="expected">Bytes the file must contain.</param>
	/// <returns>The file's path, or <see langword="null"/>.</returns>
	private static string? FindNewFileContaining(string directory, DateTime since, byte[] expected)
	{
		if (!Directory.Exists(directory))
		{
			return null;
		}

		foreach (string path in Directory.GetFiles(directory))
		{
			try
			{
				if (File.GetLastWriteTimeUtc(path) >= since && File.ReadAllBytes(path).AsSpan().IndexOf(expected) >= 0)
				{
					return path;
				}
			}
			catch (IOException)
			{
				// Still being written
			}
			catch (UnauthorizedAccessException)
			{
				// Not yet readable
			}
		}

		return null;
	}
}
