using System.Diagnostics;
using System.Text;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// A sample program running as its own process, collecting what it writes. Disposing it stops the program if it is
/// still running.
/// </summary>
public sealed class RunningSample : IAsyncDisposable
{
	private readonly Process process;                       // The program
	private readonly StringBuilder output = new StringBuilder(); // Standard output and error, as they arrive
	private readonly Lock outputLock = new Lock();          // Guards output

	/// <summary>
	/// Initialises a new instance of the <see cref="RunningSample"/> class and starts the program.
	/// </summary>
	/// <param name="startInfo">How to start it, with both outputs redirected.</param>
	/// <exception cref="InvalidOperationException">The program didn't start.</exception>
	internal RunningSample(ProcessStartInfo startInfo)
	{
		this.process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
		this.process.OutputDataReceived += (sender, eventArgs) => this.Append(eventArgs.Data);
		this.process.ErrorDataReceived += (sender, eventArgs) => this.Append(eventArgs.Data);

		if (!this.process.Start())
		{
			throw new InvalidOperationException($"{startInfo.FileName} didn't start.");
		}

		this.process.BeginOutputReadLine();
		this.process.BeginErrorReadLine();
	}

	/// <summary>
	/// Gets what the program has written so far.
	/// </summary>
	public string Output
	{
		get
		{
			lock (this.outputLock)
			{
				return this.output.ToString();
			}
		}
	}

	/// <summary>
	/// Gets a value indicating whether the program has ended.
	/// </summary>
	public bool HasExited
	{
		get
		{
			return this.process.HasExited;
		}
	}

	/// <summary>
	/// Waits for the program to end, stopping it if it takes too long.
	/// </summary>
	/// <param name="timeout">The longest to wait.</param>
	/// <param name="cancellationToken">Cancels the wait; the program is stopped.</param>
	/// <returns>How it ended; exit code -1 if it had to be stopped.</returns>
	public async Task<SampleRun> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
	{
		using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		limit.CancelAfter(timeout);

		try
		{
			await this.process.WaitForExitAsync(limit.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			this.Stop();
			this.Append($"[Stopped by the harness after {timeout.TotalSeconds} s]");

			return new SampleRun(-1, this.Output);
		}

		// The last lines can arrive just after the process ends
		this.process.WaitForExit();

		return new SampleRun(this.process.ExitCode, this.Output);
	}

	/// <summary>
	/// Waits until the program has written some text.
	/// </summary>
	/// <param name="text">The text to wait for.</param>
	/// <param name="timeout">The longest to wait.</param>
	/// <param name="cancellationToken">Cancels the wait.</param>
	/// <returns><see langword="true"/> if the text appeared in time; <see langword="false"/> if it didn't, or the
	/// program ended without writing it.</returns>
	public async Task<bool> WaitForOutputAsync(string text, TimeSpan timeout, CancellationToken cancellationToken)
	{
		DateTime deadline = DateTime.UtcNow + timeout;

		while (DateTime.UtcNow < deadline)
		{
			if (this.Output.Contains(text, StringComparison.Ordinal))
			{
				return true;
			}

			if (this.process.HasExited)
			{
				this.process.WaitForExit();

				return this.Output.Contains(text, StringComparison.Ordinal);
			}

			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
		}

		return false;
	}

	/// <summary>
	/// Stops the program if it is still running, and releases the process.
	/// </summary>
	/// <returns>A task that completes when the program has ended.</returns>
	public async ValueTask DisposeAsync()
	{
		if (!this.process.HasExited)
		{
			this.Stop();
			await this.process.WaitForExitAsync(CancellationToken.None);
		}

		this.process.Dispose();
	}

	/// <summary>
	/// Stops the program and anything it started.
	/// </summary>
	private void Stop()
	{
		try
		{
			this.process.Kill(entireProcessTree: true);
		}
		catch (InvalidOperationException)
		{
			// It ended on its own in the meantime
		}
	}

	/// <summary>
	/// Adds a line of output.
	/// </summary>
	/// <param name="line">The line, or <see langword="null"/> at the end of a stream.</param>
	private void Append(string? line)
	{
		if (line is null)
		{
			return;
		}

		lock (this.outputLock)
		{
			this.output.AppendLine(line);
		}
	}
}
