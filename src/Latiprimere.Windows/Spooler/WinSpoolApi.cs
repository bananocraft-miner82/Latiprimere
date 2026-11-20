using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.PrintTickets;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// The real Windows spooler, called through winspool.drv.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class WinSpoolApi : IWinSpool
{
	/// <summary>The registry key under which the WSD Port Monitor keeps its ports.</summary>
	private const string WsdPortsKey = @"SYSTEM\CurrentControlSet\Control\Print\Monitors\WSD Port\Ports\";

	/// <summary>The value in a WSD port's key that holds its printer's UUID.</summary>
	private const string WsdPrinterUuidValue = "Printer UUID";

	/// <summary>How WSD port names start.</summary>
	private const string WsdPortPrefix = "WSD-";

	/// <summary>The longest WSD port name read: a GUID with room for a suffix.</summary>
	private const int MaxWsdPortNameLength = 100;

	/// <summary>How many times to retry a call whose required buffer size grew between calls.</summary>
	private const int MaxBufferAttempts = 3;

	/// <summary>
	/// A spooler call that fills a caller-supplied buffer.
	/// </summary>
	/// <param name="buffer">The buffer, or null to ask for the size needed.</param>
	/// <param name="bufferSize">The buffer size in bytes.</param>
	/// <param name="bytesNeeded">The size needed.</param>
	/// <returns><see langword="true"/> on success.</returns>
	private delegate bool BufferCall(byte* buffer, uint bufferSize, out uint bytesNeeded);

	/// <summary>
	/// The GetDefaultPrinter call, which counts in characters rather than bytes.
	/// </summary>
	/// <param name="buffer">The buffer; empty to ask for the length needed.</param>
	/// <param name="length">The buffer's length in characters; on return, the length needed, including the
	/// terminating null.</param>
	/// <param name="error">The Win32 error when the call fails.</param>
	/// <returns><see langword="true"/> on success.</returns>
	internal delegate bool DefaultPrinterCall(Span<char> buffer, ref uint length, out int error);

	/// <inheritdoc/>
	public IReadOnlyList<WindowsPrinterRecord> EnumeratePrinters()
	{
		const uint flags = WinSpoolConstants.PrinterEnumLocal | WinSpoolConstants.PrinterEnumConnections;
		uint returnedCount = 0;

		byte[]? buffer = WinSpoolApi.CallWithBuffer
		(
			(byte* data, uint size, out uint needed) => WinSpoolNative.EnumPrinters
														(
															flags,
															IntPtr.Zero,
															WinSpoolConstants.PrinterInfoLevel2,
															data,
															size,
															out needed,
															out returnedCount
														),
			nameof(WinSpoolNative.EnumPrinters),
			string.Empty
		);

		// Copy each structure out of the buffer while it is pinned
		List<WindowsPrinterRecord> printers = new List<WindowsPrinterRecord>();

		if (buffer is not null)
		{
			fixed (byte* data = buffer)
			{
				PrinterInfo2* infos = (PrinterInfo2*)data;

				for (int index = 0; index < returnedCount; index++)
				{
					printers.Add(WinSpoolApi.ToPrinterRecord(infos[index]));
				}
			}
		}

		return printers;
	}

	/// <inheritdoc/>
	public IReadOnlyList<string> EnumeratePrinterNames()
	{
		// Level 4 is filled from the registry, so connections' print servers aren't asked for anything
		const uint flags = WinSpoolConstants.PrinterEnumLocal | WinSpoolConstants.PrinterEnumConnections;
		uint returnedCount = 0;

		byte[]? buffer = WinSpoolApi.CallWithBuffer
		(
			(byte* data, uint size, out uint needed) => WinSpoolNative.EnumPrinters
														(
															flags,
															IntPtr.Zero,
															WinSpoolConstants.PrinterInfoLevel4,
															data,
															size,
															out needed,
															out returnedCount
														),
			nameof(WinSpoolNative.EnumPrinters),
			string.Empty
		);

		List<string> names = new List<string>();

		if (buffer is not null)
		{
			fixed (byte* data = buffer)
			{
				PrinterInfo4* infos = (PrinterInfo4*)data;

				for (int index = 0; index < returnedCount; index++)
				{
					string? name = Marshal.PtrToStringUni(infos[index].PrinterName);

					if (name is not null)
					{
						names.Add(name);
					}
				}
			}
		}

		return names;
	}

	/// <inheritdoc/>
	public WindowsPrinterRecord? GetPrinter(string printerName)
	{
		using SafePrinterHandle? printerHandle = PrinterHandles.TryOpen(printerName);

		if (printerHandle is null)
		{
			return null;
		}

		byte[]? buffer = WinSpoolApi.CallWithBuffer
		(
			(byte* data, uint size, out uint needed) => WinSpoolNative.GetPrinter
														(
															printerHandle,
															WinSpoolConstants.PrinterInfoLevel2,
															data,
															size,
															out needed
														),
			nameof(WinSpoolNative.GetPrinter),
			printerName
		);

		fixed (byte* data = buffer)
		{
			return buffer is null ? null : WinSpoolApi.ToPrinterRecord(*(PrinterInfo2*)data);
		}
	}

	/// <inheritdoc/>
	public string? GetDefaultPrinterName()
	{
		return WinSpoolApi.ReadDefaultPrinterName
		(
			static (Span<char> buffer, ref uint length, out int error) =>
			{
				fixed (char* data = buffer)
				{
					bool isSuccess = WinSpoolNative.GetDefaultPrinter(buffer.IsEmpty ? null : data, ref length);
					error = isSuccess ? Win32ErrorCodes.Success : Marshal.GetLastPInvokeError();

					return isSuccess;
				}
			}
		);
	}

	/// <summary>
	/// Reads the default printer's name: first its length, then the name, asking again if the name grew in between.
	/// </summary>
	/// <param name="call">Calls GetDefaultPrinter.</param>
	/// <returns>The name, or <see langword="null"/> if there is no default printer.</returns>
	/// <exception cref="PrintException">The call failed, or the name kept growing.</exception>
	/// <remarks>
	/// The default can change between the two calls: with "Let Windows manage my default printer" on, Windows makes
	/// the last printer used the default, so printing from another process can make the name longer, or remove the
	/// default (seen 2026-11-16, while two test processes printed).
	/// </remarks>
	internal static string? ReadDefaultPrinterName(DefaultPrinterCall call)
	{
		char[] buffer = [];

		for (int attempt = 0; attempt < WinSpoolApi.MaxBufferAttempts; attempt++)
		{
			uint length = (uint)buffer.Length;

			if (call(buffer, ref length, out int error))
			{
				// The name ends at the terminating null
				int end = Array.IndexOf(buffer, '\0');

				return buffer.Length == 0 ? null : new string(buffer, 0, end < 0 ? buffer.Length : end);
			}

			// ERROR_FILE_NOT_FOUND means no default is set, or it was removed since the length was read
			if (error == Win32ErrorCodes.FileNotFound)
			{
				return null;
			}

			if (error != Win32ErrorCodes.InsufficientBuffer)
			{
				throw SpoolerErrors.Create(nameof(WinSpoolNative.GetDefaultPrinter), string.Empty, error);
			}

			buffer = new char[length];
		}

		throw SpoolerErrors.Create(nameof(WinSpoolNative.GetDefaultPrinter), string.Empty, Win32ErrorCodes.InsufficientBuffer);
	}

	/// <inheritdoc/>
	public int SubmitRawJob
	(
		string            printerName,
		string            documentName,
		Stream            content,
		int               copies,
		string?           outputFile,
		CancellationToken cancellationToken
	)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(copies, 1);
		cancellationToken.ThrowIfCancellationRequested();

		using SafePrinterHandle printerHandle = PrinterHandles.Open(printerName);
		long? start = copies > 1 ? content.Position : null; // Where each copy starts reading again

		// Start the job, then send the data as one page per copy: RAW data bypasses the driver, which would
		// otherwise make the copies
		uint jobId = WinSpoolApi.StartRawDocument(printerHandle, printerName, documentName, outputFile);

		try
		{
			for (int copy = 0; copy < copies; copy++)
			{
				if (start is not null)
				{
					content.Position = start.Value;
				}

				SpoolerErrors.ThrowIfFailed(WinSpoolNative.StartPagePrinter(printerHandle), nameof(WinSpoolNative.StartPagePrinter), printerName);

				WinSpoolApi.WriteAll(printerHandle, printerName, content, cancellationToken);

				SpoolerErrors.ThrowIfFailed(WinSpoolNative.EndPagePrinter(printerHandle), nameof(WinSpoolNative.EndPagePrinter), printerName);
			}
		}
		catch
		{
			// Don't leave a half-written job in the queue, whatever stopped it: a spooler error, a failing input
			// stream, or the caller's cancellation
			WinSpoolNative.SetJob(printerHandle, jobId, 0, IntPtr.Zero, WinSpoolConstants.JobControlDelete);
			WinSpoolNative.EndDocPrinter(printerHandle);

			throw;
		}

		SpoolerErrors.ThrowIfFailed(WinSpoolNative.EndDocPrinter(printerHandle), nameof(WinSpoolNative.EndDocPrinter), printerName);

		return (int)jobId;
	}

	/// <inheritdoc/>
	public WindowsJobRecord? GetJob(string printerName, int jobId)
	{
		using SafePrinterHandle printerHandle = PrinterHandles.Open(printerName);

		// ERROR_INVALID_PARAMETER means the spooler no longer has the job
		byte[]? buffer = WinSpoolApi.CallWithBuffer
		(
			(byte* data, uint size, out uint needed) => WinSpoolNative.GetJob
														(
															printerHandle,
															(uint)jobId,
															WinSpoolConstants.JobInfoLevel1,
															data,
															size,
															out needed
														),
			nameof(WinSpoolNative.GetJob),
			printerName,
			Win32ErrorCodes.InvalidParameter
		);

		fixed (byte* data = buffer)
		{
			return buffer is null ? null : WinSpoolApi.ToJobRecord(*(JobInfo1*)data);
		}
	}

	/// <inheritdoc/>
	public bool DeleteJob(string printerName, int jobId)
	{
		using SafePrinterHandle printerHandle = PrinterHandles.Open(printerName);

		if (WinSpoolNative.SetJob(printerHandle, (uint)jobId, 0, IntPtr.Zero, WinSpoolConstants.JobControlDelete))
		{
			return true;
		}

		// ERROR_INVALID_PARAMETER means the job has already left the queue
		if (Marshal.GetLastPInvokeError() == Win32ErrorCodes.InvalidParameter)
		{
			return false;
		}

		throw SpoolerErrors.Create(nameof(WinSpoolNative.SetJob), printerName);
	}

	/// <inheritdoc/>
	public WindowsCapabilitiesRecord? GetCapabilities(string printerName, bool includePrintSchema)
	{
		// The port comes from the printer's details; some drivers need it to answer
		WindowsPrinterRecord? printer = this.GetPrinter(printerName);

		return printer is null ? null : WindowsCapabilityReader.Read(printerName, printer.PortName, includePrintSchema);
	}

	/// <inheritdoc/>
	public IWindowsChangeNotification? OpenChangeNotification(string printerName, WindowsChanges changes)
	{
		ArgumentNullException.ThrowIfNull(printerName);

		return WinSpoolChangeNotification.TryOpen(printerName, changes);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The WSD Port Monitor keeps each port under <c>HKLM\SYSTEM\CurrentControlSet\Control\Print\Monitors\WSD
	/// Port\Ports\&lt;port&gt;</c>, with the printer's UUID as the string value <c>Printer UUID</c>. Reading it needs
	/// no administrator rights. Anything unexpected, such as a port name that isn't one, gives <see langword="null"/>.
	/// </remarks>
	public string? GetWsdPrinterUuid(string portName)
	{
		ArgumentNullException.ThrowIfNull(portName);

		// A port name goes into a registry path, so anything that could leave the Ports key is refused
		if (!WinSpoolApi.IsWsdPortName(portName))
		{
			return null;
		}

		try
		{
			using Microsoft.Win32.RegistryKey? port = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(WinSpoolApi.WsdPortsKey + portName);

			return port?.GetValue(WinSpoolApi.WsdPrinterUuidValue) as string;
		}
		catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
		{
			return null;
		}
	}

	/// <summary>
	/// Checks that a name is a WSD port's, <c>WSD-</c> and a GUID with any suffix of letters, digits, dots or dashes,
	/// and so safe in a registry path.
	/// </summary>
	/// <param name="portName">The port's name.</param>
	/// <returns>Whether it is.</returns>
	internal static bool IsWsdPortName(string portName)
	{
		return portName.StartsWith(WinSpoolApi.WsdPortPrefix, StringComparison.OrdinalIgnoreCase)
			&& portName.Length > WinSpoolApi.WsdPortPrefix.Length
			&& portName.Length <= WinSpoolApi.MaxWsdPortNameLength
			&& portName.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.');
	}

	/// <inheritdoc/>
	public byte[] GetDefaultPrintTicket(string printerName)
	{
		byte[] devMode = WindowsCapabilityReader.ReadDefaultDevMode(printerName);
		using PrintTicketSession session = PrintTicketSession.Open(printerName);

		return session.ConvertDevModeToTicket(devMode);
	}

	/// <inheritdoc/>
	public byte[] GetPrintCapabilities(string printerName, byte[]? baseDevMode, IReadOnlyList<byte[]> printTickets)
	{
		ArgumentNullException.ThrowIfNull(printTickets);

		byte[] devMode = baseDevMode ?? WindowsCapabilityReader.ReadDefaultDevMode(printerName);
		using PrintTicketSession session = PrintTicketSession.Open(printerName);
		byte[] ticket = session.ConvertDevModeToTicket(devMode);

		foreach (byte[] delta in printTickets)
		{
			ticket = session.Merge(ticket, delta);
		}

		return session.GetPrintCapabilities(ticket);
	}

	/// <summary>
	/// Starts a RAW job.
	/// </summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <param name="printerName">The printer name, for error messages.</param>
	/// <param name="documentName">The name shown in the print queue.</param>
	/// <param name="outputFile">A file to write to instead of the printer's port, or <see langword="null"/>.</param>
	/// <returns>The job ID.</returns>
	/// <exception cref="PrintException">The spooler refused to start the job.</exception>
	private static uint StartRawDocument(SafePrinterHandle printerHandle, string printerName, string documentName, string? outputFile)
	{
		fixed (char* documentNamePointer = documentName)
		fixed (char* outputFilePointer = outputFile)
		fixed (char* dataTypePointer = WinSpoolConstants.RawDataType)
		{
			DocInfo1 docInfo = new DocInfo1
			{
				DocumentName = (IntPtr)documentNamePointer,
				OutputFile   = (IntPtr)outputFilePointer,
				DataType     = (IntPtr)dataTypePointer
			};

			uint jobId = WinSpoolNative.StartDocPrinter(printerHandle, WinSpoolConstants.DocInfoLevel1, &docInfo);
			SpoolerErrors.ThrowIfFailed(jobId != 0, nameof(WinSpoolNative.StartDocPrinter), printerName);

			return jobId;
		}
	}

	/// <summary>
	/// Copies a stream to the current job in chunks.
	/// </summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <param name="printerName">The printer name, for error messages.</param>
	/// <param name="content">The data to send.</param>
	/// <param name="cancellationToken">Checked before each chunk.</param>
	/// <exception cref="PrintException">The spooler refused the data.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	private static void WriteAll(SafePrinterHandle printerHandle, string printerName, Stream content, CancellationToken cancellationToken)
	{
		byte[] chunk = new byte[WinSpoolConstants.WriteChunkSize];
		int bytesRead = content.Read(chunk, 0, chunk.Length);

		while (bytesRead > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// WritePrinter may accept fewer bytes than offered, so keep going until the chunk is written
			int offset = 0;

			while (offset < bytesRead)
			{
				fixed (byte* data = &chunk[offset])
				{
					bool isWritten = WinSpoolNative.WritePrinter
					(
						printerHandle,
						data,
						(uint)(bytesRead - offset),
						out uint written
					);

					SpoolerErrors.ThrowIfFailed
					(
						isWritten && written > 0,
						nameof(WinSpoolNative.WritePrinter),
						printerName
					);

					offset += (int)written;
				}
			}

			bytesRead = content.Read(chunk, 0, chunk.Length);
		}
	}

	/// <summary>
	/// Calls a buffer-filling spooler function: first for the size, then with a buffer of that size.
	/// </summary>
	/// <param name="call">The call.</param>
	/// <param name="functionName">The function name, for error messages.</param>
	/// <param name="printerName">The printer name, for error messages.</param>
	/// <param name="notFoundError">A Win32 error that means "not found", or 0 if none applies.</param>
	/// <returns>The filled buffer; <see langword="null"/> if there was nothing to return or the item was not
	/// found.</returns>
	/// <exception cref="PrintException">The call failed.</exception>
	private static byte[]? CallWithBuffer(BufferCall call, string functionName, string printerName, int notFoundError = 0)
	{
		byte[] buffer = Array.Empty<byte>();

		for (int attempt = 0; attempt < WinSpoolApi.MaxBufferAttempts; attempt++)
		{
			fixed (byte* data = buffer)
			{
				if (call(buffer.Length == 0 ? null : data, (uint)buffer.Length, out uint bytesNeeded))
				{
					return buffer.Length == 0 ? null : buffer;
				}

				int error = Marshal.GetLastPInvokeError();

				if (error != Win32ErrorCodes.InsufficientBuffer)
				{
					return error == notFoundError ? null : throw SpoolerErrors.Create(functionName, printerName, error);
				}

				buffer = new byte[bytesNeeded];
			}
		}

		throw SpoolerErrors.Create(functionName, printerName, Win32ErrorCodes.InsufficientBuffer);
	}

	/// <summary>
	/// Copies a native printer structure into a record.
	/// </summary>
	/// <param name="info">The native structure.</param>
	/// <returns>The record.</returns>
	private static WindowsPrinterRecord ToPrinterRecord(PrinterInfo2 info)
	{
		const uint workOfflineAttribute = 0x00000400; // PRINTER_ATTRIBUTE_WORK_OFFLINE

		return new WindowsPrinterRecord
		{
			Name             = Marshal.PtrToStringUni(info.PrinterName) ?? string.Empty,
			Comment          = Marshal.PtrToStringUni(info.Comment),
			Location         = Marshal.PtrToStringUni(info.Location),
			DriverName       = Marshal.PtrToStringUni(info.DriverName),
			PortName         = Marshal.PtrToStringUni(info.PortName),
			Status           = (PrinterStatusFlags)info.Status,
			IsWorkingOffline = (info.Attributes & workOfflineAttribute) != 0
		};
	}

	/// <summary>
	/// Copies a native job structure into a record.
	/// </summary>
	/// <param name="info">The native structure.</param>
	/// <returns>The record.</returns>
	private static WindowsJobRecord ToJobRecord(JobInfo1 info)
	{
		return new WindowsJobRecord
		{
			JobId        = (int)info.JobId,
			Status       = (JobStatusFlags)info.Status,
			StatusText   = Marshal.PtrToStringUni(info.StatusText),
			TotalPages   = (int)info.TotalPages,
			PagesPrinted = (int)info.PagesPrinted
		};
	}
}
