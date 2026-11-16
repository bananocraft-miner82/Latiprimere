using SharpIpp.Models.Requests;
using Xunit;

namespace Latiprimere.Cups.Tests;

public sealed class InvalidServerIppClientTests
{
	private const string Server = "printserver:notaport"; // The invalid CUPS_SERVER value

	[Theory]
	[InlineData("CUPS-Get-Printers")]
	[InlineData("CUPS-Get-Default")]
	[InlineData("Get-Printer-Attributes")]
	[InlineData("Print-Job")]
	[InlineData("Get-Job-Attributes")]
	[InlineData("Cancel-Job")]
	public async Task GivenAnyRequestWhenSentThenFailsWithPrintExceptionNamingTheVariableAndValue(string operation)
	{
		// Arrange — operation is the IPP operation to send (provided by InlineData above)
		InvalidServerIppClient client = new InvalidServerIppClient(InvalidServerIppClientTests.Server);

		// Act
		Func<Task> send = () => InvalidServerIppClientTests.SendAsync(client, operation);

		// Assert — every request says why, as any of them can be the first an application makes
		PrintException exception = await Assert.ThrowsAsync<PrintException>(send);
		Assert.Contains("CUPS_SERVER", exception.Message, StringComparison.Ordinal);
		Assert.Contains(InvalidServerIppClientTests.Server, exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Sends one IPP operation.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="operation">The operation's name.</param>
	/// <returns>A task that completes when the request has.</returns>
	private static Task SendAsync(InvalidServerIppClient client, string operation)
	{
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;

		return operation switch
		{
			"CUPS-Get-Printers" => client.GetCupsPrintersAsync(new CUPSGetPrintersRequest(), cancellationToken),
			"CUPS-Get-Default" => client.GetCupsDefaultPrinterNameAsync(new Uri("ipp://localhost:631/"), "ann", cancellationToken),
			"Get-Printer-Attributes" => client.GetPrinterAttributesAsync(new GetPrinterAttributesRequest(), cancellationToken),
			"Print-Job" => client.PrintJobAsync(new PrintJobRequest(), Latiprimere.Ipp.IppVendorAttributes.None, cancellationToken),
			"Get-Job-Attributes" => client.GetJobAttributesAsync(new GetJobAttributesRequest(), cancellationToken),
			_ => client.CancelJobAsync(new CancelJobRequest(), cancellationToken),
		};
	}
}
