using Microsoft.Extensions.Logging;

namespace Latiprimere.Tests.Fakes;

/// <summary>
/// One entry recorded by <see cref="FakeLogger{T}"/>.
/// </summary>
/// <param name="Level">The level.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The exception logged with the entry, if any.</param>
internal sealed record LoggedEntry(LogLevel Level, string Message, Exception? Exception);
