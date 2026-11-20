# Captured printer attributes

Get-Printer-Attributes responses from real print systems, kept for the mapping tests.

| Files | Source |
|---|---|
| `cups-*-printer-attributes.*` | CUPS 2.4 queues in the test container ([tools/virtual-printers](../../../tools/virtual-printers/README.md)) |
| `ippeveprinter-printer-attributes.*` | The container's `ippeveprinter` virtual IPP Everywhere printer |
| `hp-envy-5640-printer-attributes.ipp` | An HP ENVY 5640 inkjet on a home Wi-Fi network, firmware NLM2CN2023AR (2026-11-12) |

- `.txt` files are `ipptool` output, for reading.
- `quadrants-64x48.jpg` isn't a capture: a 64 × 48 JPEG of four quadrants, red, green, blue and white, made with Windows' `System.Drawing` at quality 95, for the PDF writer's JPEG pass-through (Phase 4, P4-1).
- `.ipp` files are the raw IPP responses. Tests replay them through the real decoder with `ReplayHttpMessageHandler`.

## Identifiers removed

Responses from real printers can identify the device and its network. Before a capture is added here, replace those values with placeholders **of the same length**, so the IPP encoding stays valid. Then check that the copy decodes to the same attributes.

In the HP capture:

| Value | Replaced with | Found in |
|---|---|---|
| Wi-Fi network name | `TestNetwork1` | `printer-wifi-ssid` |
| LAN address | `198.51.100.131` (a documentation address) | `printer-uri-supported`, `printer-more-info`, `printer-icons`, `printer-supply-info-uri` |
| MAC address | zeros | `printer-uuid` |
| Last six digits of the MAC | `000000` | `printer-name`, `printer-info`, `printer-dns-sd-name` |
| Serial number | `TESTSERIAL0000` | `printer-device-id` |
