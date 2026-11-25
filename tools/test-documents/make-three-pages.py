"""Writes three-pages.pdf: three A4 pages of plain text, 150 numbered lines, 50 a page.

Uses Python's standard library only. Run it from this folder: python make-three-pages.py

The text keeps 20 mm from every edge, well inside what printers can print, so a page printed on Letter, or by a
printer with wide margins on one side of the sheet, loses nothing.
"""

PAGE_WIDTH = 595
PAGE_HEIGHT = 842
MARGIN = 57  # 20 mm, in points
FONT_SIZE = 11
LEADING = 14
LINES_PER_PAGE = 50
PAGE_COUNT = 3


def page_content(page_index):
	"""The content stream of one page: its 50 numbered lines, top to bottom."""
	lines = [b'0 g', b'BT', b'/F1 %d Tf' % FONT_SIZE, b'%d TL' % LEADING]
	lines.append(b'%d %d Td' % (MARGIN, PAGE_HEIGHT - MARGIN - FONT_SIZE))

	for line_index in range(LINES_PER_PAGE):
		number = page_index * LINES_PER_PAGE + line_index + 1
		operator = b'Tj' if line_index == 0 else b"'"
		lines.append(b'(Latiprimere test document line %d) %s' % (number, operator))

	lines.append(b'ET')

	return b'\n'.join(lines) + b'\n'


def write(path):
	"""Writes the document."""
	# 1 catalog, 2 pages, 3 font, then a page and its content for each page
	page_numbers = [4 + 2 * index for index in range(PAGE_COUNT)]
	kids = b' '.join(b'%d 0 R' % number for number in page_numbers)

	objects = [
		b'<< /Type /Catalog /Pages 2 0 R >>',
		b'<< /Type /Pages /Kids [' + kids + b'] /Count %d >>' % PAGE_COUNT,
		b'<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>',
	]

	for index, number in enumerate(page_numbers):
		content = page_content(index)
		objects.append(
			b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 %d %d] /Contents %d 0 R '
			b'/Resources << /Font << /F1 3 0 R >> >> >>' % (PAGE_WIDTH, PAGE_HEIGHT, number + 1))
		objects.append(b'<< /Length %d >>\nstream\n' % len(content) + content + b'endstream')

	output = bytearray(b'%PDF-1.4\n%\xe2\xe3\xcf\xd3\n')
	offsets = []

	for number, body in enumerate(objects, start=1):
		offsets.append(len(output))
		output += b'%d 0 obj\n' % number + body + b'\nendobj\n'

	table = len(output)
	output += b'xref\n0 %d\n0000000000 65535 f \n' % (len(objects) + 1)

	for offset in offsets:
		output += b'%010d 00000 n \n' % offset

	output += b'trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n' % (len(objects) + 1, table)

	with open(path, 'wb') as file:
		file.write(output)


write('three-pages.pdf')
