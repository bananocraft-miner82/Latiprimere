"""Writes line-art.pdf and line-art-password.pdf: the same A4 page of black text, a diagonal line and a filled
circle, the second encrypted with the PDF standard security handler (revision 3, 128-bit RC4).

Uses Python's standard library only. Run it from this folder: python make-line-art.py

The page has only black on white, so a rendering without smoothing has no grey at all (ADR 0054). The encrypted
copy opens with the password test-password, or the owner password test-owner.
"""

import hashlib
import struct

USER_PASSWORD = b'test-password'
OWNER_PASSWORD = b'test-owner'
PERMISSIONS = -4  # Every permission (bits 3 to 32 set), as a signed 32-bit integer
DOCUMENT_ID = hashlib.md5(b'Latiprimere line-art test document').digest()

# The padding string of ISO 32000-1, 7.6.3.3
PADDING = bytes([
	0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
	0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
])

CONTENT = b'''0 g
BT /F1 36 Tf 72 720 Td (Latiprimere) Tj ET
BT /F1 12 Tf 72 690 Td (Black text, a line and a circle, for rendering without smoothing.) Tj ET
4 w 72 600 m 520 300 l S
q 1 0 0 1 300 200 cm
100 0 m 100 55 55 100 0 100 c -55 100 -100 55 -100 0 c -100 -55 -55 -100 0 -100 c 55 -100 100 -55 100 0 c f
Q
'''


def rc4(key, data):
	"""Encrypts or decrypts with RC4."""
	state = list(range(256))
	j = 0

	for i in range(256):
		j = (j + state[i] + key[i % len(key)]) % 256
		state[i], state[j] = state[j], state[i]

	i = j = 0
	output = bytearray()

	for byte in data:
		i = (i + 1) % 256
		j = (j + state[i]) % 256
		state[i], state[j] = state[j], state[i]
		output.append(byte ^ state[(state[i] + state[j]) % 256])

	return bytes(output)


def pad(password):
	"""Pads or truncates a password to 32 bytes."""
	return (password + PADDING)[:32]


def owner_value():
	"""Algorithm 3, revision 3."""
	digest = hashlib.md5(pad(OWNER_PASSWORD)).digest()

	for _ in range(50):
		digest = hashlib.md5(digest).digest()

	value = pad(USER_PASSWORD)

	for round_number in range(20):
		value = rc4(bytes(byte ^ round_number for byte in digest), value)

	return value


def file_key(owner):
	"""Algorithm 2, revision 3, 128 bits."""
	digest = hashlib.md5(pad(USER_PASSWORD) + owner + struct.pack('<i', PERMISSIONS) + DOCUMENT_ID).digest()

	for _ in range(50):
		digest = hashlib.md5(digest).digest()

	return digest


def user_value(key):
	"""Algorithm 5, revision 3."""
	value = rc4(key, hashlib.md5(PADDING + DOCUMENT_ID).digest())

	for round_number in range(1, 20):
		value = rc4(bytes(byte ^ round_number for byte in key), value)

	return value + bytes(16)


def object_key(key, number):
	"""Algorithm 1: the key for one object's strings and streams, generation 0."""
	return hashlib.md5(key + struct.pack('<i', number)[:3] + b'\x00\x00').digest()[:16]


def write(path, encrypted):
	"""Writes the page, encrypted or not."""
	owner = owner_value()
	key = file_key(owner)
	content = rc4(object_key(key, 4), CONTENT) if encrypted else CONTENT

	objects = [
		b'<< /Type /Catalog /Pages 2 0 R >>',
		b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
		b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R '
		b'/Resources << /Font << /F1 5 0 R >> >> >>',
		b'<< /Length ' + str(len(content)).encode() + b' >>\nstream\n' + content + b'\nendstream',
		b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
	]

	if encrypted:
		objects.append(
			b'<< /Filter /Standard /V 2 /R 3 /Length 128 /P ' + str(PERMISSIONS).encode()
			+ b' /O <' + owner.hex().encode() + b'> /U <' + user_value(key).hex().encode() + b'> >>')

	output = bytearray(b'%PDF-1.4\n%\xe2\xe3\xcf\xd3\n')
	offsets = []

	for number, body in enumerate(objects, start=1):
		offsets.append(len(output))
		output += str(number).encode() + b' 0 obj\n' + body + b'\nendobj\n'

	table = len(output)
	output += b'xref\n0 ' + str(len(objects) + 1).encode() + b'\n0000000000 65535 f \n'

	for offset in offsets:
		output += b'%010d 00000 n \n' % offset

	trailer = b'/Size ' + str(len(objects) + 1).encode() + b' /Root 1 0 R /ID [<' + DOCUMENT_ID.hex().encode() \
		+ b'> <' + DOCUMENT_ID.hex().encode() + b'>]'

	if encrypted:
		trailer += b' /Encrypt 6 0 R'

	output += b'trailer\n<< ' + trailer + b' >>\nstartxref\n' + str(table).encode() + b'\n%%EOF\n'

	with open(path, 'wb') as file:
		file.write(output)


write('line-art.pdf', encrypted=False)
write('line-art-password.pdf', encrypted=True)
