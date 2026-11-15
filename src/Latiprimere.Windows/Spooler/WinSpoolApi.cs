using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Latiprimere.Windows.Interop;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// The real Windows spooler, called through winspool.drv.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class WinSpoolApi : IWinSpool
{
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
		// Ask for the length first; ERROR_FILE_NOT_FOUND means no default is set
		uint length = 0;

		if (!WinSpoolNative.GetDefaultPrinter(null, ref length)
			&& Marshal.GetLastPInvokeError() == Win32ErrorCodes.FileNotFound)
		{
			return null;
		}

		char[] buffer = new char[length];

		fixed (char* data = buffer)
		{
			if (!WinSpoolNative.GetDefaultPrinter(data, ref length))
			{
				throw SpoolerErrors.Create(nameof(WinSpoolNative.GetDefaultPrinter), string.Empty);
			}

			// The length includes the terminating null
			return new string(data, 0, (int)length - 1);
		}
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
	public WindowsCapabilitiesRecord? GetCapabilities(string printerName)
	{
		// The port comes from the printer's details; some drivers need it to answer
		WindowsPrinterRecord? printer = this.GetPrinter(printerName);

		return printer is null ? null : WindowsCapabilityReader.Read(printerName, printer.PortName);
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
