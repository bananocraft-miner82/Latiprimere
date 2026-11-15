# Test documents

Documents used to exercise Latiprimere's print paths. They are small and safe to send to virtual printers.

| File | Pages | Content | Made with |
|---|---|---|---|
| `three-pages.pdf` | 3 | Plain text (150 numbered lines) | `cupsfilter` in the CUPS test container |
| `mixed-1-page.pdf` | 1 | A4: heading, text block, filled shapes, a rule and an embedded colour image | Ghostscript from `mixed-content.ps` |
| `mixed-100-pages.pdf` | 100 | The same page, numbered 1 to 100, for timing and memory measurements | Ghostscript from `mixed-content.ps` |

## Regenerating the mixed-content PDFs

Ghostscript is only needed to produce the files, not to use them. With Docker:

```bash
docker run --rm -v "$PWD:/work" -w /work debian:bookworm-slim sh -c \
  'apt-get update -qq && apt-get install -y -qq ghostscript && \
   gs -q -sDEVICE=pdfwrite -o mixed-1-page.pdf -c "/PageTotal 1 def" -f mixed-content.ps && \
   gs -q -sDEVICE=pdfwrite -o mixed-100-pages.pdf -c "/PageTotal 100 def" -f mixed-content.ps'
```

Run it from this folder. On Windows with Git Bash, set `MSYS_NO_PATHCONV=1` and use `$(cygpath -w "$PWD")` for the volume path.
