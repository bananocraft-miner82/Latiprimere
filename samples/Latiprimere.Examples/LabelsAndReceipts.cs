using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Latiprimere;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Labels and receipts": raw printing in the printer's own language.
/// </summary>
internal static class LabelsAndReceipts
{
	/// <summary>
	/// Prints a shipping label with a barcode on a Zebra label printer, in ZPL.
	/// </summary>
	/// <param name="printer">The label printer.</param>
	/// <param name="orderNumber">The order number to print and encode in the barcode.</param>
	/// <returns>A task that completes when the label has been sent.</returns>
	public static async Task PrintLabelAsync(IPrinter printer, string orderNumber)
	{
		string zpl = $"""
			^XA
			^CF0,40
			^FO50,50^FDOrder {orderNumber}^FS
			^FO50,110^BY3^BCN,100,Y,N,N^FD{orderNumber}^FS
			^XZ
			""";

		using MemoryStream data = new MemoryStream(Encoding.ASCII.GetBytes(zpl));
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default);

		Console.WriteLine($"Sent the label as job {job.Id}");
	}

	/// <summary>
	/// Prints a receipt on an ESC/POS receipt printer, with a bold heading, then cuts the paper.
	/// </summary>
	/// <param name="printer">The receipt printer.</param>
	/// <returns>A task that completes when the receipt has been sent.</returns>
	public static async Task PrintReceiptAsync(IPrinter printer)
	{
		byte[] reset = [0x1B, 0x40];           // ESC @: reset the printer
		byte[] boldOn = [0x1B, 0x45, 0x01];    // ESC E 1: bold on
		byte[] boldOff = [0x1B, 0x45, 0x00];   // ESC E 0: bold off
		byte[] cut = [0x1D, 0x56, 0x41, 0x03]; // GS V A 3: feed the paper, then cut it

		using MemoryStream data = new MemoryStream();

		data.Write(reset);
		data.Write(boldOn);
		data.Write(Encoding.ASCII.GetBytes("Corner Cafe\n"));
		data.Write(boldOff);
		data.Write(Encoding.ASCII.GetBytes("Flat white       3.20\nCroissant        2.80\n\nTotal            6.00\n"));
		data.Write(cut);
		data.Position = 0;

		await printer.SubmitAsync(PrintContent.FromRaw(data), new PrintOptions { JobName = "Receipt" });
	}
}
