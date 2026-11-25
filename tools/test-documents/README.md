# Test documents

Documents used to exercise Latiprimere's print paths. They are small and safe to send to virtual printers.

| File | Pages | Content | Made with |
|---|---|---|---|
| `three-pages.pdf` | 3 | A4: plain text, 150 numbered lines, 20 mm from every edge | `make-three-pages.py`, Python's standard library only |
| `mixed-1-page.pdf` | 1 | A4: heading, text block, filled shapes, a rule and an embedded colour image | Ghostscript from `mixed-content.ps` |
| `mixed-100-pages.pdf` | 100 | The same page, numbered 1 to 100, for timing and memory measurements | Ghostscript from `mixed-content.ps` |
| `line-art.pdf` | 1 | A4: black text, a diagonal line and a filled circle, only black on white, for rendering without smoothing | `make-line-art.py`, Python's standard library only |
| `line-art-password.pdf` | 1 | The same page, encrypted (128-bit RC4). Password `test-password`, owner password `test-owner` | `make-line-art.py` |
| `photo-300x200.png` | 1 | A 300 × 200 colour image at 96 dpi: a gradient, a black bar along the top, a red square bottom right, and a transparent disc | `make-images.py`, Python's standard library only |
| `text-2-pages.txt` | 2 | UTF-8 text: a tabbed table with accented words, a line long enough to wrap, 30 numbered lines, then a form feed and a second page of Windows-1252 punctuation | By hand |
| `photo-300x200.jpg` | 1 | The same image as a JPEG, quality 90, its disc white | `make-images.ps1`, Windows' `System.Drawing`, from the PNG |

## Regenerating the mixed-content PDFs

Ghostscript is only needed to produce the files, not to use them. With Docker:

```bash
docker run --rm -v "$PWD:/work" -w /work debian:bookworm-slim sh -c \
  'apt-get update -qq && apt-get install -y -qq ghostscript && \
   gs -q -sDEVICE=pdfwrite -o mixed-1-page.pdf -c "/PageTotal 1 def" -f mixed-content.ps && \
   gs -q -sDEVICE=pdfwrite -o mixed-100-pages.pdf -c "/PageTotal 100 def" -f mixed-content.ps'
```

Run it from this folder. On Windows with Git Bash, set `MSYS_NO_PATHCONV=1` and use `$(cygpath -w "$PWD")` for the volume path.

## Regenerating the three-page text

Run `python make-three-pages.py` from this folder. The text keeps 20 mm from every edge, so nothing is cut off on Letter paper or by a printer with a wide margin on one side of the sheet.

## Regenerating the line art

Run `python make-line-art.py` from this folder. It writes both files, the second encrypted with the PDF standard security handler, revision 3, which every PDF reader opens with the password.

## Regenerating the images

Run `python make-images.py`, then, on Windows, `./make-images.ps1`, from this folder. The PNG needs nothing beyond Python; the JPEG needs Windows' `System.Drawing`, which draws the PNG over white first, as JPEG has no transparency.
