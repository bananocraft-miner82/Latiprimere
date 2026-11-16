using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Resources;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// Reads the <c>windows:</c> keys of <see cref="PrintOptions.VendorOptions"/> (ADR 0026).
/// </summary>
internal static class WindowsVendorOptions
{
	/// <summary>The prefix the Windows print system reads.</summary>
	internal const string Prefix = "windows:";

	/// <summary>A whole DEVMODE saved from the printer's driver, in base64.</summary>
	internal const string DevModeKey = "windows:devmode";

	/// <summary>Where <c>dmSize</c> is in a DEVMODEW: after the 32-character device name and two versions.</summary>
	private const int SizeOffset = 68;

	/// <summary>Where <c>dmDriverExtra</c> is.</summary>
	private const int DriverExtraOffset = 70;

	/// <summary>
	/// The smallest public part accepted: up to <c>dmFormName</c>, which covers every member Latiprimere sets, so
	/// its changes land inside what the driver reads as the DEVMODE.
	/// </summary>
	private static readonly int MinimumSize = (int)Marshal.OffsetOf<DevMode>(nameof(DevMode.FormName));

	/// <summary>
	/// Reads the saved DEVMODE, after checking every <c>windows:</c> key is one Windows supports.
	/// </summary>
	/// <param name="vendorOptions">The vendor options.</param>
	/// <returns>The DEVMODE, with the driver's private data, or <see langword="null"/> if none is given.</returns>
	/// <exception cref="NotSupportedException">A <c>windows:</c> key isn't one Windows supports.</exception>
	/// <exception cref="ArgumentException">The DEVMODE isn't base64, or isn't a DEVMODE.</exception>
	internal static byte[]? ReadDevMode(IReadOnlyDictionary<string, string> vendorOptions)
	{
		ArgumentNullException.ThrowIfNull(vendorOptions);

		// Unlike IPP, Windows has nowhere to pass an unknown setting on to
		string? unsupported = vendorOptions.Keys
			.Where(key => key.StartsWith(WindowsVendorOptions.Prefix, StringComparison.Ordinal))
			.Where(key => key != WindowsVendorOptions.DevModeKey)
			.Order(StringComparer.Ordinal)
			.FirstOrDefault();

		if (unsupported is not null)
		{
			string message = ExceptionMessages.FormatVendorOptionNotSupported(unsupported, WindowsVendorOptions.DevModeKey);

			throw new NotSupportedException(message);
		}

		return vendorOptions.TryGetValue(WindowsVendorOptions.DevModeKey, out string? encoded)
			? WindowsVendorOptions.DecodeDevMode(encoded)
			: null;
	}

	/// <summary>
	/// Decodes a saved DEVMODE and checks its sizes add up.
	/// </summary>
	/// <param name="encoded">The DEVMODE in base64.</param>
	/// <returns>The DEVMODE.</returns>
	/// <exception cref="ArgumentException">The value isn't base64, or isn't a DEVMODE.</exception>
	private static byte[] DecodeDevMode(string encoded)
	{
		byte[] devMode;

		try
		{
			devMode = Convert.FromBase64String(encoded);
		}
		catch (FormatException exception)
		{
			throw new ArgumentException(ExceptionMessages.DevModeNotBase64, exception);
		}

		// dmSize and dmDriverExtra describe the buffer; a memory block from GlobalAlloc may be a little longer
		int size = devMode.Length > WindowsVendorOptions.DriverExtraOffset + 1
			? BinaryPrimitives.ReadUInt16LittleEndian(devMode.AsSpan(WindowsVendorOptions.SizeOffset))
			: 0;

		int driverExtra = size > 0
			? BinaryPrimitives.ReadUInt16LittleEndian(devMode.AsSpan(WindowsVendorOptions.DriverExtraOffset))
			: 0;

		if (size < WindowsVendorOptions.MinimumSize || size + driverExtra > devMode.Length)
		{
			string message = ExceptionMessages.FormatDevModeInvalid(devMode.Length, size, driverExtra);

			throw new ArgumentException(message);
		}

		return devMode;
	}
}
