using Latiprimere.Ipp.Mapping;
using Latiprimere.Ipp.Testing.Builders;
using SharpIpp.Protocol.Models;
using Xunit;
using IppPrinterState = SharpIpp.Protocol.Models.PrinterState;

namespace Latiprimere.Ipp.Tests.Mapping;

public sealed class IppPrinterInfoMapperTests
{
	[Fact]
	public void GivenFullAttributesWhenToPrinterInfoThenMapsEveryField()
	{
		// Arrange
		PrinterDescriptionAttributes attributes = new PrinterAttributesBuilder().Build();

		// Act
		PrinterInfo info = IppPrinterInfoMapper.ToPrinterInfo(attributes, "cups:Virtual-IPP", "fallback", true);

		// Assert
		Assert.Equal("cups:Virtual-IPP", info.Id);
		Assert.Equal("Virtual-IPP", info.Name);
		Assert.Equal("Virtual IPP Everywhere printer", info.Description);
		Assert.Equal("Test container", info.Location);
		Assert.Equal("Printer - IPP Everywhere", info.MakeAndModel);
		Assert.Equal(Latiprimere.PrinterState.Idle, info.State);
		Assert.True(info.IsDefault);
	}

	[Fact]
	public void GivenNoNameOrDescriptionWhenToPrinterInfoThenUsesFallbackNameAndNulls()
	{
		// Arrange
		PrinterDescriptionAttributes attributes = new PrinterAttributesBuilder()
			.WithName(null)
			.WithoutDescription()
			.WithState(null)
			.Build();

		// Act
		PrinterInfo info = IppPrinterInfoMapper.ToPrinterInfo(attributes, "id", "Fallback", false);

		// Assert
		Assert.Equal("Fallback", info.Name);
		Assert.Null(info.Description);
		Assert.Null(info.Location);
		Assert.Null(info.MakeAndModel);
		Assert.Equal(Latiprimere.PrinterState.Unknown, info.State);
		Assert.False(info.IsDefault);
	}

	[Theory]
	[InlineData(IppPrinterState.Idle, Latiprimere.PrinterState.Idle)]
	[InlineData(IppPrinterState.Processing, Latiprimere.PrinterState.Processing)]
	[InlineData(IppPrinterState.Stopped, Latiprimere.PrinterState.Stopped)]
	public void GivenIppPrinterStateWhenToPrinterStateThenMapsToMatchingState
	(
		IppPrinterState ippState,
		Latiprimere.PrinterState expectedState
	)
	{
		// Arrange — each IPP printer-state value and its Latiprimere equivalent (provided by InlineData above)
		IppPrinterState? state = ippState;

		// Act
		Latiprimere.PrinterState mappedState = IppPrinterInfoMapper.ToPrinterState(state);

		// Assert
		Assert.Equal(expectedState, mappedState);
	}

	[Fact]
	public void GivenUnrecognisedPrinterStateWhenToPrinterStateThenReturnsUnknown()
	{
		// Arrange
		IppPrinterState? state = (IppPrinterState)99;

		// Act
		Latiprimere.PrinterState mappedState = IppPrinterInfoMapper.ToPrinterState(state);

		// Assert
		Assert.Equal(Latiprimere.PrinterState.Unknown, mappedState);
	}
}
