namespace Latiprimere.Testing.Fixtures;

/// <summary>
/// Waits for a virtual printer to finish writing an output file.
/// </summary>
/// <remarks>
/// A job can be reported finished a moment before the printer's output file is complete, and the file may still be
/// open while it is written, so the file is read until its content matches or the time runs out.
/// </remarks>
public static class OutputFileWaiter
{
	/// <summary>
	/// The longest to wait for the file.
	/// </summary>
	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200); // How often the file is read

	/// <summary>
	/// Waits until a file holds exactly the expected bytes.
	/// </summary>
	/// <param name="path">The output file.</param>
	/// <param name="expected">The bytes the file should hold.</param>
	/// <param name="cancellationToken">The test's cancellation token.</param>
	/// <returns>The file's last content read, or <see langword="null"/> if it never existed; equal to
	/// <paramref name="expected"/> unless the time ran out.</returns>
	/// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/>, empty or whitespace.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="expected"/> is <see langword="null"/>.</exception>
	public static Task<byte[]?> WaitForContentAsync(string path, byte[] expected, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(expected);

		return OutputFileWaiter.WaitAsync(path, content => content.AsSpan().SequenceEqual(expected), cancellationToken);
	}

	/// <summary>
	/// Waits until some file in a folder holds exactly the expected bytes, for printers that name their files
	/// themselves, such as the capture server on a Mac.
	/// </summary>
	/// <param name="directory">The output folder.</param>
	/// <param name="expected">The bytes a file should hold. Make them unique to the test, so only its own job can
	/// match.</param>
	/// <param name="cancellationToken">The test's cancellation token.</param>
	/// <returns>The content of the file that matched, or <see langword="null"/> if none did in time.</returns>
	/// <exception cref="ArgumentException"><paramref name="directory"/> is <see langword="null"/>, empty or whitespace.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="expected"/> is <see langword="null"/>.</exception>
	public static async Task<byte[]?> WaitForFileWithContentAsync(string directory, byte[] expected, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentNullException.ThrowIfNull(expected);

		DateTime deadline = DateTime.UtcNow + OutputFileWaiter.Timeout;

		while (true)
		{
			byte[]? content = OutputFileWaiter.FindFileWithContent(directory, expected);

			if (content is not null || DateTime.UtcNow >= deadline)
			{
				return content;
			}

			await Task.Delay(OutputFileWaiter.PollInterval, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Waits until a file exists and isn't empty.
	/// </summary>
	/// <param name="path">The output file.</param>
	/// <param name="cancellationToken">The test's cancellation token.</param>
	/// <returns>The file's last content read, or <see langword="null"/> if it never existed; not empty unless the
	/// time ran out.</returns>
	/// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/>, empty or whitespace.</exception>
	public static Task<byte[]?> WaitForAnyContentAsync(string path, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		return OutputFileWaiter.WaitAsync(path, content => content.Length > 0, cancellationToken);
	}

	/// <summary>
	/// Reads a file until its content is complete or the time runs out.
	/// </summary>
	/// <param name="path">The output file.</param>
	/// <param name="isComplete">Says whether the content read is complete.</param>
	/// <param name="cancellationToken">The test's cancellation token.</param>
	/// <returns>The file's last content read, or <see langword="null"/> if it never existed.</returns>
	private static async Task<byte[]?> WaitAsync(string path, Func<byte[], bool> isComplete, CancellationToken cancellationToken)
	{
		DateTime deadline = DateTime.UtcNow + OutputFileWaiter.Timeout;
		byte[]? content = null;

		while (true)
		{
			content = OutputFileWaiter.TryRead(path) ?? content;

			if ((content is not null && isComplete(content)) || DateTime.UtcNow >= deadline)
			{
				return content;
			}

			await Task.Delay(OutputFileWaiter.PollInterval, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Looks through a folder's files for one that holds exactly the expected bytes.
	/// </summary>
	/// <param name="directory">The folder.</param>
	/// <param name="expected">The bytes to look for.</param>
	/// <returns>The matching file's content, or <see langword="null"/> if no file matches yet.</returns>
	private static byte[]? FindFileWithContent(string directory, byte[] expected)
	{
		IEnumerable<string> paths;

		try
		{
			paths = Directory.GetFiles(directory);
		}
		catch (DirectoryNotFoundException)
		{
			return null;
		}

		foreach (string path in paths)
		{
			byte[]? content = OutputFileWaiter.TryRead(path);

			if (content is not null && content.AsSpan().SequenceEqual(expected))
			{
				return content;
			}
		}

		return null;
	}

	/// <summary>
	/// Reads a file, allowing others to keep writing it.
	/// </summary>
	/// <param name="path">The file.</param>
	/// <returns>Its content, or <see langword="null"/> if it doesn't exist or can't be read yet.</returns>
	private static byte[]? TryRead(string path)
	{
		try
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using MemoryStream copy = new MemoryStream();
			stream.CopyTo(copy);

			return copy.ToArray();
		}
		catch (FileNotFoundException)
		{
			return null;
		}
		catch (DirectoryNotFoundException)
		{
			return null;
		}
		catch (IOException)
		{
			// Still being written by the printer
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			// Created by the print system, and not yet readable
			return null;
		}
	}
}
