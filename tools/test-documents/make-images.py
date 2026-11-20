"""Writes photo-300x200.png, the test image for image printing, with Python's standard library only.

A 300 x 200 colour image with an alpha channel: a horizontal colour gradient, a black bar along the top, a white
disc whose inside is fully transparent, and a red square in the bottom right, so orientation, colour and
transparency can all be seen on paper. photo-300x200.jpg is made from it with make-images.ps1.

Usage: python make-images.py   (from this folder)
"""

import struct
import zlib

WIDTH = 300
HEIGHT = 200


def pixel(x, y):
    """Returns the red, green, blue and alpha of one pixel."""
    # The top bar, so up is easy to see
    if y < 20:
        return 0, 0, 0, 255

    # A red square in the bottom right, so left from right is easy to see
    if x >= 240 and y >= 150:
        return 220, 20, 20, 255

    # A clear disc, which prints as the paper's white
    if (x - 100) ** 2 + (y - 110) ** 2 < 40 ** 2:
        return 0, 0, 0, 0

    # A gradient from blue on the left to yellow on the right
    shade = x * 255 // (WIDTH - 1)
    return shade, shade, 255 - shade, 255


def chunk(kind, data):
    """Returns a chunk: its length, type, data and CRC."""
    body = kind + data
    return struct.pack('>I', len(data)) + body + struct.pack('>I', zlib.crc32(body) & 0xFFFFFFFF)


def main():
    rows = bytearray()

    for y in range(HEIGHT):
        rows.append(0)  # No filter

        for x in range(WIDTH):
            rows.extend(pixel(x, y))

    header = struct.pack('>IIBBBBB', WIDTH, HEIGHT, 8, 6, 0, 0, 0)
    resolution = struct.pack('>IIB', 3780, 3780, 1)  # 96 dpi, in pixels per metre

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', header)
           + chunk(b'pHYs', resolution)
           + chunk(b'IDAT', zlib.compress(bytes(rows), 9))
           + chunk(b'IEND', b''))

    with open('photo-300x200.png', 'wb') as file:
        file.write(png)


if __name__ == '__main__':
    main()
