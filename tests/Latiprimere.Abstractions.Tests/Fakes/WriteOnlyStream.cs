namespace Latiprimere.Abstractions.Tests.Fakes;

/// <summary>
/// An in-memory stream that reports it cannot be read, for testing stream validation.
/// </summary>
internal sealed class WriteOnlyStream : MemoryStream
{
	/// <summary>
	/// Gets a value indicating whether the stream can be read. Always <see langword="false"/>.
	/// </summary>
	public override bool CanRead
	{
		get
		{
			return false;
		}
	}
}
