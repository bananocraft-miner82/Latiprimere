using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Spooler;

public sealed class DriverQuirksTests
{
	[Theory]
	[InlineData("Microsoft Print To PDF", true)]
	[InlineData("MICROSOFT PRINT TO PDF", true)]
	[InlineData("Microsoft XPS Document Writer v4", false)]
	[InlineData("Generic / Text Only", false)]
	[InlineData(null, false)]
	public void GivenDriverNameWhenDropsRawDataCheckedThenOnlyKnownDroppersAreRefused(string? driverName, bool expected)
	{
		// Arrange — driverName is a driver, or none known, and expected whether it drops raw data (provided by
		// InlineData above)
		string? name = driverName;

		// Act
		bool dropsRawData = DriverQuirks.DropsRawData(name);

		// Assert
		Assert.Equal(expected, dropsRawData);
	}
}
