using System.Globalization;
using Latiprimere.EndToEnd;

namespace Latiprimere.TestHarness.Commands;

/// <summary>
/// The person at the keyboard, asked on the console. When answers are piped in rather than typed, each is echoed,
/// so the transcript reads as a conversation.
/// </summary>
internal sealed class ConsolePerson : IPerson
{
	/// <summary>
	/// Asks the person to agree; only "y" or "yes" is a yes.
	/// </summary>
	/// <param name="question">The question.</param>
	/// <param name="cancellationToken">Not used: the console can't be interrupted mid-line.</param>
	/// <returns><see langword="true"/> for a yes.</returns>
	public Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken)
	{
		string answer = ConsolePerson.Prompt($"{question} [y/N] ").ToUpperInvariant();

		return Task.FromResult(answer is "Y" or "YES");
	}

	/// <summary>
	/// Asks whether something is so: yes, no (then what was seen instead), or skip.
	/// </summary>
	/// <param name="question">The question.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The answer.</returns>
	public Task<PersonAnswer> AskAsync(string question, CancellationToken cancellationToken)
	{
		while (true)
		{
			string answer = ConsolePerson.Prompt($"{question} [y = yes, n = no, s = can't tell] ").ToUpperInvariant();

			switch (answer)
			{
				case "Y" or "YES":
					return Task.FromResult(new PersonAnswer(PersonReply.Yes, null));

				case "N" or "NO":
					string seen = ConsolePerson.Prompt("What did you see instead? ");

					return Task.FromResult(new PersonAnswer(PersonReply.No, seen.Length == 0 ? null : seen));

				case "S" or "SKIP" or "":
					return Task.FromResult(new PersonAnswer(PersonReply.Skipped, null));
			}
		}
	}

	/// <summary>
	/// Asks the person to choose by number.
	/// </summary>
	/// <param name="question">The question.</param>
	/// <param name="choices">The choices.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The chosen index, or <see langword="null"/> for none.</returns>
	public Task<int?> ChooseAsync(string question, IReadOnlyList<string> choices, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(choices);

		Console.WriteLine();
		Console.WriteLine($"?? {question}");

		for (int index = 0; index < choices.Count; index++)
		{
			Console.WriteLine($"     {index + 1}. {choices[index]}");
		}

		while (true)
		{
			string answer = ConsolePerson.Prompt("Number, or Enter for none: ", newLine: false);

			if (answer.Length == 0)
			{
				return Task.FromResult<int?>(null);
			}

			if (int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= 1 && number <= choices.Count)
			{
				return Task.FromResult<int?>(number - 1);
			}
		}
	}

	/// <summary>
	/// Shows the person a message.
	/// </summary>
	/// <param name="message">The message, one or more lines.</param>
	public void Tell(string message)
	{
		Console.WriteLine();

		foreach (string line in (message ?? string.Empty).Split('\n'))
		{
			Console.WriteLine("   " + line.TrimEnd('\r'));
		}
	}

	/// <summary>
	/// Shows a prompt and reads a line, trimmed. The end of the input counts as an empty answer.
	/// </summary>
	/// <param name="prompt">The prompt.</param>
	/// <param name="newLine">Whether to start the prompt on a new line.</param>
	/// <returns>The answer.</returns>
	private static string Prompt(string prompt, bool newLine = true)
	{
		if (newLine)
		{
			Console.WriteLine();
		}

		Console.Write($"?? {prompt}");
		string answer = Console.ReadLine() ?? string.Empty;

		if (Console.IsInputRedirected)
		{
			Console.WriteLine(answer);
		}

		return answer.Trim();
	}
}
