---
title: Labels and receipts
description: Send raw printer data, such as ZPL to a Zebra label printer or ESC/POS to a receipt printer.
sidebar:
  order: 3
---

Label and receipt printers usually have their own languages: ZPL for Zebra label printers, ESC/POS for most receipt printers. To use one, build the commands yourself and send them with `PrintContent.FromRaw`. Latiprimere passes the bytes to the printer unchanged.

## A shipping label in ZPL

This prints an order number and a Code 128 barcode of it:

```csharp
static async Task PrintLabelAsync(IPrinter printer, string orderNumber)
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
```

`^XA` and `^XZ` start and end the label. `^FO` places a field, `^CF` sets the font, and `^BC` draws the barcode. Zebra's ZPL programming guide covers the rest.

## A receipt in ESC/POS

ESC/POS mixes text with control codes. This prints a bold heading and two lines, then feeds and cuts the paper:

```csharp
static async Task PrintReceiptAsync(IPrinter printer)
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
```

The example uses plain ASCII. For characters such as `£` or `é`, choose a code page the printer supports with `ESC t` and encode the text in it. Printers differ, so check the manual for yours.

## Things to know about raw printing

- **The printer must understand the data.** Latiprimere can't check it. ZPL sent to an office laser printer may come out as pages of text, or not at all.
- **Use a seekable stream,** such as a `MemoryStream` or a file. A network printer that is busy turns the job away, and only a seekable stream can be sent again. On Windows, more than one copy of raw data also needs one, because the data is sent once for each copy.
- **`PrintOptions` settings may have no effect.** The printer follows the commands in the data, so set paper, darkness and similar settings in the data itself.
- **Virtual printers refuse raw data.** Microsoft Print to PDF would silently produce nothing, so Latiprimere refuses with `NotSupportedException` before anything is queued.
- **Some printers or queues don't accept raw data** at all. The job is then rejected with a `PrintException`.

## Label printers on the network

Many label and receipt printers sit on the network with no driver installed. If they support IPP, you can print to them directly, without the Windows spooler or CUPS: see [Network printers over IPP](/docs/print-systems/network-printers/).

The code on this page is in [LabelsAndReceipts.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/LabelsAndReceipts.cs).
