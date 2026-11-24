using System.Runtime.Versioning;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.PrintTickets;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// Reads a printer driver's capabilities with DeviceCapabilities, and its defaults from its default DEVMODE.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe class WindowsCapabilityReader
{
	/// <summary>
	/// Reads a printer's capabilities.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="portName">The printer's port, which some drivers need, or <see langword="null"/>.</param>
	/// <param name="includePrintSchema">Whether to read the Print Capabilities and default Print Ticket too.</param>
	/// <returns>The capabilities.</returns>
	/// <exception cref="PrintException">The printer couldn't be opened or its DEVMODE read.</exception>
	internal static WindowsCapabilitiesRecord Read(string printerName, string? portName, bool includePrintSchema = true)
	{
		// The default DEVMODE is read once, for the defaults and as the starting point of the Print Capabilities
		byte[] defaultDevMode = WindowsCapabilityReader.ReadDefaultDevMode(printerName);
		DefaultSettings defaults = WindowsCapabilityReader.ReadDefaults(defaultDevMode);
		int copies = WinSpoolNative.DeviceCapabilities(printerName, portName, DeviceCapabilityConstants.Copies, null, null);
		PrintSchemaRead printSchema = includePrintSchema
			? WindowsCapabilityReader.ReadPrintSchema(printerName, defaultDevMode)
			: new PrintSchemaRead(null, null, null);

		return new WindowsCapabilitiesRecord
		{
			Papers            = WindowsCapabilityReader.ReadPapers(printerName, portName),
			Bins              = WindowsCapabilityReader.ReadBins(printerName, portName),
			MediaTypes        = WindowsCapabilityReader.ReadMediaTypes(printerName, portName),
			MinExtent         = WindowsCapabilityReader.ReadExtent(printerName, portName, DeviceCapabilityConstants.MinExtent),
			MaxExtent         = WindowsCapabilityReader.ReadExtent(printerName, portName, DeviceCapabilityConstants.MaxExtent),
			CanDuplex         = WindowsCapabilityReader.ReadFlag
			(
				printerName,
				portName,
				DeviceCapabilityConstants.Duplex
			),
			IsColorDevice     = WindowsCapabilityReader.ReadFlag
			(
				printerName,
				portName,
				DeviceCapabilityConstants.ColorDevice
			),
			MaxCopies         = copies > 0 ? copies : null,
			CanPrintLandscape = WinSpoolNative.DeviceCapabilities
			(
				printerName, portName, DeviceCapabilityConstants.Orientation, null, null
			) > 0,
			DefaultPaperId    = defaults.PaperId,
			DefaultDuplex     = defaults.Duplex,
			DefaultColor      = defaults.Color,
			PrintCapabilities = printSchema.PrintCapabilities,
			DefaultPrintTicket = printSchema.DefaultTicket,
			PrintSchemaFailure = printSchema.Failure
		};
	}

	/// <summary>
	/// Reads the driver's Print Capabilities and default Print Ticket, in one Print Ticket session (ADR 0032).
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="defaultDevMode">The driver's default DEVMODE, already read.</param>
	/// <returns>Both documents, as UTF-8 XML; or, for a driver without Print Ticket support, neither but the reason,
	/// for the caller to log, since the other capabilities are still read.</returns>
	private static PrintSchemaRead ReadPrintSchema(string printerName, byte[] defaultDevMode)
	{
		try
		{
			using PrintTicketSession session = PrintTicketSession.Open(printerName);
			byte[] ticket = session.ConvertDevModeToTicket(defaultDevMode);

			return new PrintSchemaRead(session.GetPrintCapabilities(ticket), ticket, null);
		}
		catch (Exception exception) when (exception is PrintException or ArgumentException)
		{
			return new PrintSchemaRead(null, null, exception);
		}
	}

	/// <summary>
	/// Reads the paper sizes: identifiers, names and dimensions, which the driver returns as three parallel lists.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="portName">The port, or <see langword="null"/>.</param>
	/// <returns>The papers; empty if the driver doesn't list them.</returns>
	private static List<WindowsPaperRecord> ReadPapers(string printerName, string? portName)
	{
		ushort[] ids = WindowsCapabilityReader.ReadArray<ushort>(printerName, portName, DeviceCapabilityConstants.Papers, 1);

		string[] names = WindowsCapabilityReader.ReadNames
		(
			printerName, portName, DeviceCapabilityConstants.PaperNames, DeviceCapabilityConstants.PaperNameLength
		);

		int[] sizes = WindowsCapabilityReader.ReadArray<int>(printerName, portName, DeviceCapabilityConstants.PaperSize, 2);

		return WindowsCapabilityReader.CombinePapers(ids, names, sizes);
	}

	/// <summary>
	/// Combines the three parallel lists a driver returns for its paper sizes, using only the entries all three
	/// agree on.
	/// </summary>
	/// <param name="ids">The DMPAPER_* identifiers.</param>
	/// <param name="names">The names.</param>
	/// <param name="sizes">The widths and heights, in tenths of a millimetre, two per paper.</param>
	/// <returns>The papers.</returns>
	internal static List<WindowsPaperRecord> CombinePapers(ushort[] ids, string[] names, int[] sizes)
	{
		int count = Math.Min(ids.Length, Math.Min(names.Length, sizes.Length / 2));
		List<WindowsPaperRecord> papers = new List<WindowsPaperRecord>(count);

		for (int index = 0; index < count; index++)
		{
			papers.Add(new WindowsPaperRecord
			{
				Id                       = unchecked((short)ids[index]),
				Name                     = names[index],
				WidthTenthsOfMillimetre  = sizes[index * 2],
				HeightTenthsOfMillimetre = sizes[(index * 2) + 1]
			});
		}

		return papers;
	}

	/// <summary>
	/// Reads the paper sources: identifiers and names, returned as two parallel lists.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="portName">The port, or <see langword="null"/>.</param>
	/// <returns>The sources; empty if the driver doesn't list them.</returns>
	private static List<WindowsBinRecord> ReadBins(string printerName, string? portName)
	{
		ushort[] ids = WindowsCapabilityReader.ReadArray<ushort>(printerName, portName, DeviceCapabilityConstants.Bins, 1);

		string[] names = WindowsCapabilityReader.ReadNames
		(
			printerName, portName, DeviceCapabilityConstants.BinNames, DeviceCapabilityConstants.BinNameLength
		);

		return WindowsCapabilityReader.CombineBins(ids, names);
	}

	/// <summary>
	/// Reads the paper types: numbers and names, returned as two parallel lists (ADR 0056).
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="portName">The port, or <see langword="null"/>.</param>
	/// <returns>The types; empty if the driver doesn't list them.</returns>
	private static List<WindowsMediaTypeRecord> ReadMediaTypes(string printerName, string? portName)
	{
		uint[] ids = WindowsCapabilityReader.ReadArray<uint>(printerName, portName, DeviceCapabilityConstants.MediaTypes, 1);

		string[] names = WindowsCapabilityReader.ReadNames
		(
			printerName, portName, DeviceCapabilityConstants.MediaTypeNames, DeviceCapabilityConstants.MediaTypeNameLength
		);

		return WindowsCapabilityReader.CombineMediaTypes(ids, names);
	}

	/// <summary>
	/// Combines the two parallel lists a driver returns for its paper types, using only the entries both agree on.
	/// </summary>
	/// <param name="ids">The DMMEDIA_* numbers.</param>
	/// <param name="names">The names.</param>
	/// <returns>The types.</returns>
	internal static List<WindowsMediaTypeRecord> CombineMediaTypes(uint[] ids, string[] names)
	{
		int count = Math.Min(ids.Length, names.Length);
		List<WindowsMediaTypeRecord> types = new List<WindowsMediaTypeRecord>(count);

		for (int index = 0; index < count; index++)
		{
			types.Add(new WindowsMediaTypeRecord { Id = ids[index], Name = names[index] });
		}

		return types;
	}

	/// <summary>
	/// Reads a custom paper limit, which DeviceCapabilities returns packed in its result.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="portName">The port, or <see langword="null"/>.</param>
	/// <param name="capability">DC_MINEXTENT or DC_MAXEXTENT.</param>
	/// <returns>The width and length in tenths of a millimetre, or <see langword="null"/> if the driver gives
	/// none.</returns>
	private static (short Width, short Length)? ReadExtent(string printerName, string? portName, ushort capability)
	{
		int packed = WinSpoolNative.DeviceCapabilities(printerName, portName, capability, null, null);

		return WindowsCapabilityReader.UnpackExtent(packed);
	}

	/// <summary>
	/// Unpacks a custom paper limit: the width in the low word, the length in the high.
	/// </summary>
	/// <param name="packed">DeviceCapabilities' result.</param>
	/// <returns>The width and length in tenths of a millimetre, or <see langword="null"/> for a failure (-1) or a
	/// zero or negative size.</returns>
	internal static (short Width, short Length)? UnpackExtent(int packed)
	{
		short width = unchecked((short)(packed & 0xFFFF));
		short length = unchecked((short)((packed >> 16) & 0xFFFF));

		return packed != -1 && width > 0 && length > 0 ? (width, length) : null;
	}

	/// <summary>
	/// Combines the two parallel lists a driver returns for its paper sources, using only the entries both agree on.
	/// </summary>
	/// <param name="ids">The DMBIN_* identifiers.</param>
	/// <param name="names">The names.</param>
	/// <returns>The sources.</returns>
	internal static List<WindowsBinRecord> CombineBins(ushort[] ids, string[] names)
	{
		int count = Math.Min(ids.Length, names.Length);
		List<WindowsBinRecord> bins = new List<WindowsBinRecord>(count);

		for (int index = 0; index < count; index++)
		{
			bins.Add(new WindowsBinRecord { Id = unchecked((short)ids[index]), Name = names[index] });
		}

		return bins;
	}

	/// <summary>
	/// Reads a yes-or-no capability.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="portName">The port, or <see langword="null"/>.</param>
	/// <param name="capability">The DC_* capability.</param>
	/// <returns>Whether the driver answered 1.</returns>
	private static bool ReadFlag(string printerName, string? portName, ushort capability)
	{
		return WinSpoolNative.DeviceCapabilities(printerName, portName, capability, null, null) == 1;
	}

	/// <summary>
	/// Reads a list capability made of fixed-size elements.
	/// </summary>
	/// <typeparam name="T">The type of each value.</typeparam>
	/// <param name="printerName">The printer name.</param>
	/// <param name="portName">The port, or <see langword="null"/>.</param>
	/// <param name="capability">The DC_* capability.</param>
	/// <param name="valuesPerElement">How many values make one element, such as 2 for a POINT.</param>
	/// <returns>The values; empty if the driver doesn't support the capability.</returns>
	private static T[] ReadArray<T>(string printerName, string? portName, ushort capability, int valuesPerElement)
		where T : unmanaged
	{
		int count = WinSpoolNative.DeviceCapabilities(printerName, portName, capability, null, null);

		if (count <= 0)
		{
			return Array.Empty<T>();
		}

		// DeviceCapabilities takes no buffer size, so a driver whose list grows between the two calls would write
		// past a buffer sized exactly; twice the count leaves room for that
		int capacity = count * 2;
		T[] values = new T[capacity * valuesPerElement];

		fixed (T* data = values)
		{
			int written = WinSpoolNative.DeviceCapabilities(printerName, portName, capability, data, null);

			return written <= 0 ? Array.Empty<T>() : values[..(Math.Min(written, capacity) * valuesPerElement)];
		}
	}

	/// <summary>
	/// Reads a list of fixed-length names, each padded with nulls.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="portName">The port, or <see langword="null"/>.</param>
	/// <param name="capability">The DC_* capability.</param>
	/// <param name="nameLength">The length of each name, in characters.</param>
	/// <returns>The names; empty if the driver doesn't support the capability.</returns>
	private static string[] ReadNames(string printerName, string? portName, ushort capability, int nameLength)
	{
		char[] characters = WindowsCapabilityReader.ReadArray<char>(printerName, portName, capability, nameLength);

		return WindowsCapabilityReader.SplitNames(characters, nameLength);
	}

	/// <summary>
	/// Splits the fixed-length name slots a driver fills into names.
	/// </summary>
	/// <param name="characters">The slots, one after another; a partial slot at the end is ignored.</param>
	/// <param name="nameLength">The length of each slot, in characters.</param>
	/// <returns>The names.</returns>
	internal static string[] SplitNames(char[] characters, int nameLength)
	{
		string[] names = new string[characters.Length / nameLength];

		for (int index = 0; index < names.Length; index++)
		{
			// A name that fills its slot has no terminating null
			ReadOnlySpan<char> slot = characters.AsSpan(index * nameLength, nameLength);
			int end = slot.IndexOf('\0');
			names[index] = new string(end < 0 ? slot : slot[..end]);
		}

		return names;
	}

	/// <summary>
	/// Reads the driver's default DEVMODE, with its private data.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <returns>The DEVMODE, as an application would save it for <c>windows:devmode</c>.</returns>
	/// <exception cref="PrintException">The printer couldn't be opened or its DEVMODE read.</exception>
	internal static byte[] ReadDefaultDevMode(string printerName)
	{
		using SafePrinterHandle printerHandle = PrinterHandles.Open(printerName);
		int size = WinSpoolNative.DocumentProperties(IntPtr.Zero, printerHandle, printerName, null, null, 0);
		SpoolerErrors.ThrowIfFailed(size > 0, nameof(WinSpoolNative.DocumentProperties), printerName);

		byte[] buffer = new byte[size];

		fixed (byte* data = buffer)
		{
			int result = WinSpoolNative.DocumentProperties
			(
				IntPtr.Zero, printerHandle, printerName, (DevMode*)data, null, DevModeConstants.ModeOutBuffer
			);

			SpoolerErrors.ThrowIfFailed(result > 0, nameof(WinSpoolNative.DocumentProperties), printerName);
		}

		return buffer;
	}

	/// <summary>
	/// Reads the paper size, duplex and colour from the driver's default DEVMODE.
	/// </summary>
	/// <param name="buffer">The default DEVMODE.</param>
	/// <returns>The defaults the DEVMODE sets.</returns>
	private static DefaultSettings ReadDefaults(byte[] buffer)
	{
		fixed (byte* data = buffer)
		{
			DevMode* devMode = (DevMode*)data;

			// Fields says which members the driver filled in; the rest are meaningless
			uint fields = devMode->Fields;

			return new DefaultSettings
			{
				PaperId = (fields & DevModeConstants.FieldPaperSize) != 0 ? devMode->PaperSize : null,
				Duplex  = (fields & DevModeConstants.FieldDuplex) != 0 ? devMode->Duplex : null,
				Color   = (fields & DevModeConstants.FieldColor) != 0 ? devMode->Color : null
			};
		}
	}
}
