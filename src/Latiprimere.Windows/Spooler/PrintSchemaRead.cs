namespace Latiprimere.Windows.Spooler;

/// <summary>
/// What reading a driver's Print Capabilities and default Print Ticket gave: both documents, or why neither could be
/// read (ADR 0032).
/// </summary>
/// <param name="PrintCapabilities">The Print Capabilities document, as UTF-8 XML, or <see langword="null"/>.</param>
/// <param name="DefaultTicket">The default Print Ticket, as UTF-8 XML, or <see langword="null"/>.</param>
/// <param name="Failure">Why they couldn't be read, or <see langword="null"/>.</param>
internal sealed record PrintSchemaRead(byte[]? PrintCapabilities, byte[]? DefaultTicket, Exception? Failure);
