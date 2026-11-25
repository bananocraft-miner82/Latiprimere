namespace Latiprimere;

/// <summary>
/// Enumerates the ways a printer can make itself known when asked to with <see cref="IPrinter.IdentifyAsync"/>, as
/// IPP's <c>identify-actions</c> has them.
/// </summary>
/// <remarks>
/// Later versions may add ways, as IPP's list may grow. Ignore one you don't know.
/// </remarks>
public enum IdentifyAction
{
	/// <summary>
	/// Show a message on the printer's display: the one given to <see cref="IPrinter.IdentifyAsync"/>, or the
	/// printer's own.
	/// </summary>
	Display = 1,

	/// <summary>
	/// Flash a light, or the display.
	/// </summary>
	Flash = 2,

	/// <summary>
	/// Make a sound, such as a beep.
	/// </summary>
	Sound = 3,

	/// <summary>
	/// Speak the message aloud.
	/// </summary>
	Speak = 4,
}
