namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// The files a printer capture wrote, and the values in it that may identify the device or its network.
/// </summary>
/// <param name="IppPath">The raw Get-Printer-Attributes response.</param>
/// <param name="TextPath">The same attributes as text, one per line.</param>
/// <param name="AttributeCount">How many printer attributes the printer sent.</param>
/// <param name="IdentifyingValues">Values to replace, with placeholders of the same length, before the capture is
/// added to <c>tests/Latiprimere.Ipp.Tests/Data</c>: <c>name = value</c>, one each.</param>
public sealed record IppCaptureResult(string IppPath, string TextPath, int AttributeCount, IReadOnlyList<string> IdentifyingValues);
