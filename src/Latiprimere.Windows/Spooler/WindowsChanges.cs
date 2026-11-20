namespace Latiprimere.Windows.Spooler;

/// <summary>
/// The changes a spooler change notification is signalled for.
/// </summary>
internal enum WindowsChanges
{
	/// <summary>The printer's settings or status: <c>PRINTER_CHANGE_PRINTER</c>.</summary>
	Printer = 0,

	/// <summary>Jobs added to, changed in or gone from the queue: <c>PRINTER_CHANGE_JOB</c>.</summary>
	Jobs = 1,
}
