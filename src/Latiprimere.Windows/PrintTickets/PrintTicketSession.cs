using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Resources;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows.PrintTickets;

/// <summary>
/// One printer's Print Ticket provider, open for a few calls of the Windows Print Ticket API (ADR 0031, ADR 0032).
/// </summary>
/// <remarks>
/// <para>
/// A provider belongs to the thread that opened it, so a session is opened, used and disposed on one thread, within
/// one synchronous call. It initialises COM for that thread as a multithreaded apartment if the thread has no
/// apartment yet, and undoes that when disposed.
/// </para>
/// <para>
/// Tickets and Print Capabilities documents go in and out as UTF-8 XML bytes.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed unsafe class PrintTicketSession : IDisposable
{
	private readonly string printerName;                     // The printer, for messages
	private readonly SafePrintTicketProviderHandle provider; // The open provider
	private readonly bool uninitialiseCom;                   // Whether this session initialised COM
	private bool disposed;                                   // Whether Dispose has run, so COM is undone once only

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintTicketSession"/> class.
	/// </summary>
	/// <param name="printerName">The printer.</param>
	/// <param name="provider">Its open provider.</param>
	/// <param name="uninitialiseCom">Whether disposing must balance a <c>CoInitializeEx</c>.</param>
	private PrintTicketSession(string printerName, SafePrintTicketProviderHandle provider, bool uninitialiseCom)
	{
		this.printerName = printerName;
		this.provider = provider;
		this.uninitialiseCom = uninitialiseCom;
	}

	/// <summary>
	/// Opens a printer's Print Ticket provider.
	/// </summary>
	/// <param name="printerName">The printer.</param>
	/// <returns>The session.</returns>
	/// <exception cref="PrintException">The provider couldn't be opened, as for an old driver without Print Ticket
	/// support, or Windows has no Print Ticket API.</exception>
	/// <exception cref="PrinterNotFoundException">No printer has that name.</exception>
	internal static PrintTicketSession Open(string printerName)
	{
		ArgumentNullException.ThrowIfNull(printerName);

		// Calls run on thread-pool threads, which may not have joined an apartment yet
		int initialised = PrintTicketNative.CoInitializeEx(IntPtr.Zero, PrintTicketConstants.MultiThreaded);

		if (initialised < 0 && initialised != PrintTicketConstants.ChangedMode)
		{
			throw PrintTicketSession.CreateException(nameof(PrintTicketNative.CoInitializeEx), printerName, initialised, IntPtr.Zero);
		}

		bool uninitialiseCom = initialised >= 0;

		try
		{
			int result = PrintTicketSession.OpenProvider(printerName, out SafePrintTicketProviderHandle provider);

			if (result < 0)
			{
				provider.Dispose();

				throw PrintTicketSession.CreateException(nameof(PrintTicketNative.PTOpenProvider), printerName, result, IntPtr.Zero);
			}

			return new PrintTicketSession(printerName, provider, uninitialiseCom);
		}
		catch
		{
			if (uninitialiseCom)
			{
				PrintTicketNative.CoUninitialize();
			}

			throw;
		}
	}

	/// <summary>
	/// Opens the provider, turning a Windows without the Print Ticket API into a <see cref="PrintException"/>.
	/// </summary>
	/// <param name="printerName">The printer.</param>
	/// <param name="provider">The provider, on success.</param>
	/// <returns>The HRESULT.</returns>
	/// <exception cref="PrintException">prntvpt.dll, or the function, isn't there, as on some stripped-down Windows
	/// images.</exception>
	private static int OpenProvider(string printerName, out SafePrintTicketProviderHandle provider)
	{
		try
		{
			return PrintTicketNative.PTOpenProvider(printerName, PrintTicketConstants.SchemaVersion, out provider);
		}
		catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
		{
			throw new PrintException(ExceptionMessages.FormatPrintTicketApiMissing(printerName, exception.Message), exception);
		}
	}

	/// <summary>
	/// Writes a DEVMODE's settings as a full Print Ticket.
	/// </summary>
	/// <param name="devMode">The DEVMODE, with the driver's private data.</param>
	/// <returns>The ticket.</returns>
	/// <exception cref="PrintException">The call failed.</exception>
	internal byte[] ConvertDevModeToTicket(ReadOnlySpan<byte> devMode)
	{
		using SafeComStreamHandle ticket = this.CreateStream();

		fixed (byte* devModeData = devMode)
		{
			DevMode* mode = (DevMode*)devModeData;
			uint size = (uint)(mode->Size + mode->DriverExtra);

			int result = PrintTicketNative.PTConvertDevModeToPrintTicket(this.provider, size, mode, PrintTicketConstants.JobScope, ticket);
			this.ThrowIfFailed(result, nameof(PrintTicketNative.PTConvertDevModeToPrintTicket), IntPtr.Zero);
		}

		return this.ReadAll(ticket);
	}

	/// <summary>
	/// Merges a ticket onto a base ticket; the driver resolves any conflicts, as it does for a DEVMODE.
	/// </summary>
	/// <param name="baseTicket">The full ticket to start from.</param>
	/// <param name="deltaTicket">The ticket to merge in, usually naming only what it changes.</param>
	/// <returns>The merged full ticket.</returns>
	/// <exception cref="ArgumentException">The driver finds the ticket malformed.</exception>
	/// <exception cref="PrintException">The call failed.</exception>
	internal byte[] Merge(ReadOnlySpan<byte> baseTicket, ReadOnlySpan<byte> deltaTicket)
	{
		using SafeComStreamHandle baseStream = this.CreateStream(baseTicket);
		using SafeComStreamHandle deltaStream = this.CreateStream(deltaTicket);
		using SafeComStreamHandle merged = this.CreateStream();
		IntPtr errorMessage = IntPtr.Zero;

		try
		{
			// S_PT_CONFLICT_RESOLVED is a success: the driver changed settings that conflicted
			int result = PrintTicketNative.PTMergeAndValidatePrintTicket
			(
				this.provider, baseStream, deltaStream, PrintTicketConstants.JobScope, merged, &errorMessage
			);

			this.ThrowIfFailed(result, nameof(PrintTicketNative.PTMergeAndValidatePrintTicket), errorMessage);
		}
		finally
		{
			PrintTicketNative.SysFreeString(errorMessage);
		}

		return this.ReadAll(merged);
	}

	/// <summary>
	/// Turns a full ticket into a DEVMODE for the driver; the user's defaults fill anything it leaves out.
	/// </summary>
	/// <param name="ticket">The ticket.</param>
	/// <returns>The DEVMODE, with the driver's private data.</returns>
	/// <exception cref="ArgumentException">The driver finds the ticket malformed.</exception>
	/// <exception cref="PrintException">The call failed.</exception>
	internal byte[] ConvertTicketToDevMode(ReadOnlySpan<byte> ticket)
	{
		using SafeComStreamHandle ticketStream = this.CreateStream(ticket);
		uint size = 0;
		DevMode* converted = null;
		IntPtr errorMessage = IntPtr.Zero;

		try
		{
			int result = PrintTicketNative.PTConvertPrintTicketToDevMode
			(
				this.provider,
				ticketStream,
				PrintTicketConstants.UserDefaultDevMode,
				PrintTicketConstants.JobScope,
				&size,
				&converted,
				&errorMessage
			);

			this.ThrowIfFailed(result, nameof(PrintTicketNative.PTConvertPrintTicketToDevMode), errorMessage);

			return new ReadOnlySpan<byte>(converted, (int)size).ToArray();
		}
		finally
		{
			PrintTicketNative.SysFreeString(errorMessage);

			// Already copied, so a failure to free it changes nothing for the caller
			if (converted is not null)
			{
				_ = PrintTicketNative.PTReleaseMemory(converted);
			}
		}
	}

	/// <summary>
	/// Reads the printer's Print Capabilities: its features and their options, the driver's own among them.
	/// </summary>
	/// <param name="ticket">A full ticket whose choices constrain the options, such as the media types a chosen
	/// tray takes.</param>
	/// <returns>The Print Capabilities document.</returns>
	/// <exception cref="ArgumentException">The driver finds the ticket malformed.</exception>
	/// <exception cref="PrintException">The call failed.</exception>
	internal byte[] GetPrintCapabilities(ReadOnlySpan<byte> ticket)
	{
		using SafeComStreamHandle ticketStream = this.CreateStream(ticket);
		using SafeComStreamHandle capabilities = this.CreateStream();
		IntPtr errorMessage = IntPtr.Zero;

		try
		{
			int result = PrintTicketNative.PTGetPrintCapabilities(this.provider, ticketStream, capabilities, &errorMessage);
			this.ThrowIfFailed(result, nameof(PrintTicketNative.PTGetPrintCapabilities), errorMessage);
		}
		finally
		{
			PrintTicketNative.SysFreeString(errorMessage);
		}

		return this.ReadAll(capabilities);
	}

	/// <summary>
	/// Closes the provider, and undoes this session's COM initialisation.
	/// </summary>
	public void Dispose()
	{
		// A second call would uninitialise COM once more than it was initialised, on a thread others still use
		if (this.disposed)
		{
			return;
		}

		this.disposed = true;
		this.provider.Dispose();

		if (this.uninitialiseCom)
		{
			PrintTicketNative.CoUninitialize();
		}
	}

	/// <summary>
	/// Creates the exception for a failed call.
	/// </summary>
	/// <param name="functionName">The function that failed.</param>
	/// <param name="printerName">The printer.</param>
	/// <param name="result">The HRESULT.</param>
	/// <param name="errorMessage">The BSTR the function gave to describe it, or null.</param>
	/// <returns>
	/// An <see cref="ArgumentException"/> for a ticket the driver finds malformed; for a Win32 error, the exception
	/// <see cref="SpoolerErrors"/> maps it to, such as <see cref="PrinterNotFoundException"/>; otherwise a
	/// <see cref="PrintException"/> with the HRESULT as its native error code.
	/// </returns>
	private static Exception CreateException(string functionName, string printerName, int result, IntPtr errorMessage)
	{
		string? driverMessage = errorMessage == IntPtr.Zero ? null : Marshal.PtrToStringBSTR(errorMessage);
		string description = string.IsNullOrWhiteSpace(driverMessage) ? new Win32Exception(result).Message : driverMessage;

		if (result is PrintTicketConstants.PrintTicketFormatError or PrintTicketConstants.DeltaPrintTicketFormatError)
		{
			return new ArgumentException(ExceptionMessages.FormatPrintTicketRefused(printerName, description));
		}

		if ((result & unchecked((int)0xFFFF0000)) == PrintTicketConstants.Win32Facility)
		{
			return SpoolerErrors.Create(functionName, printerName, result & 0xFFFF);
		}

		string code = result.ToString("X8", CultureInfo.InvariantCulture);

		return new PrintException(ExceptionMessages.FormatPrintTicketCallFailed(printerName, functionName, code, description))
		{
			NativeErrorCode = result
		};
	}

	/// <summary>
	/// Throws if an HRESULT is a failure.
	/// </summary>
	/// <param name="result">The HRESULT.</param>
	/// <param name="functionName">The function that returned it.</param>
	/// <param name="errorMessage">The BSTR the function gave to describe it, or null.</param>
	private void ThrowIfFailed(int result, string functionName, IntPtr errorMessage)
	{
		if (result < 0)
		{
			throw PrintTicketSession.CreateException(functionName, this.printerName, result, errorMessage);
		}
	}

	/// <summary>
	/// Creates a memory stream, holding bytes if any are given, positioned at its start.
	/// </summary>
	/// <param name="content">The bytes to put in it.</param>
	/// <returns>The stream.</returns>
	private SafeComStreamHandle CreateStream(ReadOnlySpan<byte> content = default)
	{
		int result = PrintTicketNative.CreateStreamOnHGlobal(IntPtr.Zero, deleteOnRelease: true, out SafeComStreamHandle stream);

		try
		{
			this.ThrowIfFailed(result, nameof(PrintTicketNative.CreateStreamOnHGlobal), IntPtr.Zero);

			if (!content.IsEmpty)
			{
				this.ThrowIfFailed(stream.Write(content), "IStream::Write", IntPtr.Zero);
				this.ThrowIfFailed(stream.Rewind(), "IStream::Seek", IntPtr.Zero);
			}

			return stream;
		}
		catch
		{
			stream.Dispose();

			throw;
		}
	}

	/// <summary>
	/// Reads a stream the Print Ticket API wrote.
	/// </summary>
	/// <param name="stream">The stream.</param>
	/// <returns>Its bytes.</returns>
	private byte[] ReadAll(SafeComStreamHandle stream)
	{
		byte[] content = stream.ReadAll(out int result);
		this.ThrowIfFailed(result, "IStream::Read", IntPtr.Zero);

		return content;
	}
}
