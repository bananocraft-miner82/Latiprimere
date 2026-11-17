using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Latiprimere;
using LatiprimereExamples.Tests.Fakes;
using Xunit;

namespace LatiprimereExamples.Tests;

public sealed class InvoicePrinterTests
{
	private const string OfficeLaserId = "windows:Office Laser";

	// Only the fake printer reads the document, so it needn't be a real PDF
	private static readonly byte[] InvoicePdf = Encoding.ASCII.GetBytes("%PDF-1.7 invoice 1042");

	[Fact]
	public async Task GivenAnInvoiceWhenPrintedThenSendsItAsATwoSidedPdfNamedAfterTheInvoice()
	{
		// Arrange
		FakePrinter officeLaser = new FakePrinter(InvoicePrinterTests.OfficeLaserId, "Office Laser");
		InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(new FakePrinterCatalog(officeLaser));

		// Act
		await InvoicePrinterTests.PrintInvoiceAsync(invoices, InvoicePrinterTests.OfficeLaserId);

		// Assert
		SubmittedJob job = Assert.Single(officeLaser.SubmittedJobs);
		Assert.Equal(DocumentFormat.Pdf, job.Format);
		Assert.Equal("Invoice 1042", job.Options.JobName);
		Assert.Equal(DuplexMode.LongEdge, job.Options.Duplex);
		Assert.Equal(InvoicePrinterTests.InvoicePdf, job.Document.ToArray());
	}

	[Fact]
	public async Task GivenAJobThatPrintsWhenPrintedThenReturnsTrue()
	{
		// Arrange
		FakePrinter officeLaser = new FakePrinter(InvoicePrinterTests.OfficeLaserId, "Office Laser")
		{
			JobStates = [PrintJobState.Pending, PrintJobState.Processing, PrintJobState.Completed],
		};

		InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(new FakePrinterCatalog(officeLaser));

		// Act
		bool printed = await InvoicePrinterTests.PrintInvoiceAsync(invoices, InvoicePrinterTests.OfficeLaserId);

		// Assert
		Assert.True(printed);
	}

	[Fact]
	public async Task GivenAJobThePrinterAbortsWhenPrintedThenReturnsFalse()
	{
		// Arrange
		FakePrinter officeLaser = new FakePrinter(InvoicePrinterTests.OfficeLaserId, "Office Laser")
		{
			JobStates = [PrintJobState.Processing, PrintJobState.Aborted],
		};

		InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(new FakePrinterCatalog(officeLaser));

		// Act
		bool printed = await InvoicePrinterTests.PrintInvoiceAsync(invoices, InvoicePrinterTests.OfficeLaserId);

		// Assert
		Assert.False(printed);
	}

	[Fact]
	public async Task GivenTheSavedPrinterHasGoneWhenPrintedThenUsesTheDefaultPrinter()
	{
		// Arrange — the catalog no longer has the saved printer, but has a default
		FakePrinter reception = new FakePrinter("windows:Reception", "Reception");
		FakePrinterCatalog catalog = new FakePrinterCatalog(reception) { DefaultPrinter = reception };
		InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(catalog);

		// Act
		bool printed = await InvoicePrinterTests.PrintInvoiceAsync(invoices, InvoicePrinterTests.OfficeLaserId);

		// Assert
		Assert.True(printed);
		Assert.Single(reception.SubmittedJobs);
	}

	[Fact]
	public async Task GivenNoSavedOrDefaultPrinterWhenPrintedThenThrowsPrinterNotFoundException()
	{
		// Arrange
		InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(new FakePrinterCatalog());

		// Act
		Func<Task> print = () => InvoicePrinterTests.PrintInvoiceAsync(invoices, InvoicePrinterTests.OfficeLaserId);

		// Assert
		PrinterNotFoundException exception = await Assert.ThrowsAsync<PrinterNotFoundException>(print);
		Assert.Equal(InvoicePrinterTests.OfficeLaserId, exception.PrinterId);
	}

	[Fact]
	public async Task GivenAPrinterThatTurnsTheJobAwayWhenPrintedThenThrowsPrintException()
	{
		// Arrange
		FakePrinter officeLaser = new FakePrinter(InvoicePrinterTests.OfficeLaserId, "Office Laser")
		{
			Rejection = new PrintException("The printer is out of paper."),
		};

		InvoicePrinter invoices = InvoicePrinterTests.CreateInvoicePrinter(new FakePrinterCatalog(officeLaser));

		// Act
		Func<Task> print = () => InvoicePrinterTests.PrintInvoiceAsync(invoices, InvoicePrinterTests.OfficeLaserId);

		// Assert
		await Assert.ThrowsAsync<PrintException>(print);
		Assert.Empty(officeLaser.SubmittedJobs);
	}

	/// <summary>
	/// Creates the invoice printer, checking jobs every millisecond so the tests don't wait.
	/// </summary>
	/// <param name="catalog">The fake catalog.</param>
	/// <returns>The invoice printer.</returns>
	private static InvoicePrinter CreateInvoicePrinter(FakePrinterCatalog catalog)
	{
		return new InvoicePrinter(catalog) { PollInterval = TimeSpan.FromMilliseconds(1) };
	}

	/// <summary>
	/// Prints invoice 1042 to the given printer.
	/// </summary>
	/// <param name="invoices">The invoice printer.</param>
	/// <param name="printerId">The ID of the printer to print to.</param>
	/// <returns>Whether the invoice printed.</returns>
	private static async Task<bool> PrintInvoiceAsync(InvoicePrinter invoices, string printerId)
	{
		using MemoryStream invoicePdf = new MemoryStream(InvoicePrinterTests.InvoicePdf, writable: false);

		return await invoices.PrintAsync(printerId, "1042", invoicePdf, TestContext.Current.CancellationToken);
	}
}
