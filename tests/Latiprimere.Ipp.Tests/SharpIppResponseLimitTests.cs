using System.Net;
using System.Net.Http.Headers;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Tests.Fakes;
using Xunit;

namespace Latiprimere.Ipp.Tests;

/// <summary>
/// A printer's answer is read only as far as SharpIppNext's own limits allow: about 5,000 values of about a kilobyte,
/// each no longer than IPP allows. A hostile printer, or one anyone on the network announced, can't make Latiprimere
/// read an answer without end, as these tests show. Latiprimere relies on those limits rather than adding its own, so
/// if an update to SharpIppNext drops them, these fail.
/// </summary>
public sealed class SharpIppResponseLimitTests
{
	/// <summary>How much an endless answer gives before ending, if it's read that far.</summary>
	private const long EndlessLength = 64L * 1024 * 1024;

	/// <summary>The most of an endless answer that may be read: a little above the 5 MB SharpIppNext stops at.</summary>
	private const long MaxBytesRead = 8L * 1024 * 1024;

	private static readonly Uri PrinterUri = new Uri("ipp://printer.test/ipp/print");

	[Theory]
	[InlineData(EndlessIppResponseShape.MoreValues)]
	[InlineData(EndlessIppResponseShape.MoreAttributes)]
	[InlineData(EndlessIppResponseShape.MoreGroups)]
	public async Task GivenAnswerThatNeverEndsWhenQueriedThenStopsReadingWithinAFewMegabytesAndThrowsPrintException
	(
		EndlessIppResponseShape shape
	)
	{
		// Arrange — attributes of a kilobyte each, as long as IPP allows text to be, without end
		EndlessIppResponseStream answer = new EndlessIppResponseStream(SharpIppResponseLimitTests.EndlessLength, shape, 1000);

		// Act
		Exception? exception = await SharpIppResponseLimitTests.QueryAsync(answer);

		// Assert
		Assert.IsType<PrintException>(exception);
		Assert.InRange(answer.BytesRead, 1, SharpIppResponseLimitTests.MaxBytesRead);
	}

	[Fact]
	public async Task GivenValueLongerThanIppAllowsWhenQueriedThenStopsAtItAndThrowsPrintException()
	{
		// Arrange — values of 60,000 bytes, far past the 1,023 IPP allows text
		EndlessIppResponseStream answer = new EndlessIppResponseStream
		(
			SharpIppResponseLimitTests.EndlessLength,
			EndlessIppResponseShape.MoreValues,
			60_000
		);

		// Act
		Exception? exception = await SharpIppResponseLimitTests.QueryAsync(answer);

		// Assert — refused at the first long value, not after reading them all
		Assert.IsType<PrintException>(exception);
		Assert.InRange(answer.BytesRead, 1, 64 * 1024);
	}

	/// <summary>
	/// Asks a printer that gives an answer for its name, through SharpIppNext as Latiprimere uses it.
	/// </summary>
	/// <param name="answer">The printer's answer.</param>
	/// <returns>What the query threw, if anything.</returns>
	private static async Task<Exception?> QueryAsync(Stream answer)
	{
		DelegateHttpMessageHandler printer = new DelegateHttpMessageHandler((request, cancellationToken) =>
		{
			StreamContent content = new StreamContent(answer);
			content.Headers.ContentType = new MediaTypeHeaderValue("application/ipp");

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
		});

		using SharpIppClientAdapter client = new SharpIppClientAdapter(new HttpClient(printer, disposeHandler: true));
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).Build();

		return await Record.ExceptionAsync
		(
			() => IppPrinterQueries.GetAttributesAsync
			(
				context,
				SharpIppResponseLimitTests.PrinterUri,
				["printer-name"],
				TestContext.Current.CancellationToken
			)
		);
	}
}
