using SharpIpp.Protocol.Models;
using IppPrinterState = SharpIpp.Protocol.Models.PrinterState;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Maps IPP printer description attributes to <see cref="PrinterInfo"/>.
/// </summary>
internal static class IppPrinterInfoMapper
{
	/// <summary>
	/// Creates printer information from IPP printer attributes.
	/// </summary>
	/// <param name="attributes">The printer's IPP attributes.</param>
	/// <param name="printerId">The Latiprimere identifier to give the printer.</param>
	/// <param name="fallbackName">The name to use if the printer does not report <c>printer-name</c>.</param>
	/// <param name="isDefault">Whether the printer is its print system's default.</param>
	/// <returns>The printer information.</returns>
	internal static PrinterInfo ToPrinterInfo(PrinterDescriptionAttributes attributes, string printerId, string fallbackName, bool isDefault)
	{
		ArgumentNullException.ThrowIfNull(attributes);

		return new PrinterInfo
		{
			Id           = printerId,
			Name         = IppValueReader.ReadString(attributes.PrinterName) ?? fallbackName,
			Description  = IppValueReader.ReadString(attributes.PrinterInfo),
			Location     = IppValueReader.ReadString(attributes.PrinterLocation),
			MakeAndModel = IppValueReader.ReadString(attributes.PrinterMakeAndModel),
			State        = IppPrinterInfoMapper.ToPrinterState(IppValueReader.ReadValue(attributes.PrinterState)),
			IsDefault    = isDefault
		};
	}

	/// <summary>
	/// Maps an IPP <c>printer-state</c> value.
	/// </summary>
	/// <param name="state">The IPP printer state, or <see langword="null"/> if not reported.</param>
	/// <returns>The Latiprimere printer state.</returns>
	internal static Latiprimere.PrinterState ToPrinterState(IppPrinterState? state)
	{
		return state switch
		{
			IppPrinterState.Idle       => Latiprimere.PrinterState.Idle,
			IppPrinterState.Processing => Latiprimere.PrinterState.Processing,
			IppPrinterState.Stopped    => Latiprimere.PrinterState.Stopped,
			_                          => Latiprimere.PrinterState.Unknown,
		};
	}
}
