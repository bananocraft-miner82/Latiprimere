namespace Latiprimere.EndToEnd;

/// <summary>
/// The person at the keyboard in the console harness's manual mode, who confirms each job before it prints on paper
/// and says what came out. Nothing else provides one, so scenarios that need a person never run unattended.
/// </summary>
public interface IPerson
{
	/// <summary>
	/// Asks the person to agree to something, such as printing a job on paper. Anything but a yes is a no.
	/// </summary>
	/// <param name="question">The question, ending with a question mark.</param>
	/// <param name="cancellationToken">Cancels the question.</param>
	/// <returns><see langword="true"/> if the person said yes.</returns>
	Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken);

	/// <summary>
	/// Asks the person whether something they can see is so, such as "Is the page in grey only?".
	/// </summary>
	/// <param name="question">The question, ending with a question mark.</param>
	/// <param name="cancellationToken">Cancels the question.</param>
	/// <returns>Yes, no (with what they saw instead), or not answered.</returns>
	Task<PersonAnswer> AskAsync(string question, CancellationToken cancellationToken);

	/// <summary>
	/// Asks the person to choose one of several things, such as a tray.
	/// </summary>
	/// <param name="question">The question.</param>
	/// <param name="choices">The choices, shown numbered from 1.</param>
	/// <param name="cancellationToken">Cancels the question.</param>
	/// <returns>The chosen index, or <see langword="null"/> if the person chose none.</returns>
	Task<int?> ChooseAsync(string question, IReadOnlyList<string> choices, CancellationToken cancellationToken);

	/// <summary>
	/// Tells the person something, such as what is about to print.
	/// </summary>
	/// <param name="message">The message.</param>
	void Tell(string message);
}
