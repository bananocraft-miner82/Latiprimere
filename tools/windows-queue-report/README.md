# Windows queue report

[Get-PrinterQueueReport.ps1](Get-PrinterQueueReport.ps1) records how Windows has set up each printer queue. It changes nothing and needs no administrator rights.

```powershell
.\tools\windows-queue-report\Get-PrinterQueueReport.ps1
.\tools\windows-queue-report\Get-PrinterQueueReport.ps1 -PrinterName 'Office Laser' -OutputPath "$env:TEMP\office.json"
```

It writes one JSON file, `printer-queue-report.json` by default, with this for each queue:

- `Get-Printer`, `Get-PrinterPort` and `Get-PrinterDriver`, with every property;
- the queue's spooler registry key, and its port's key under its port monitor;
- the queue's Plug and Play device, and the other devices of the same physical printer, with every device property;
- the port monitors installed, and the Windows version.

It takes about 15 seconds.

## Why

Latiprimere sends a Windows queue's PDF jobs straight to its printer over IPP when it knows the printer's IPP address ([ADR 0027](../../docs/decisions/0027-direct-ipp-for-windows-queues.md)). For a queue added by URL, the port name is that address. For other queues it isn't, and Microsoft doesn't document how Windows sets up the queues it creates itself. Reports from real queues show what Windows records, and so what Latiprimere could use.

What the reports have shown so far is in [ADR 0027](../../docs/decisions/0027-direct-ipp-for-windows-queues.md), under "Follow-up".

## Before sharing a report

A report can identify the printers and the network:

- IP addresses and host names;
- MAC addresses, often inside UUIDs and queue names;
- device IDs and serial numbers.

Read it first, and replace what you'd rather not share. If you replace a UUID, use the same replacement everywhere it appears, so the report still shows which values match.
