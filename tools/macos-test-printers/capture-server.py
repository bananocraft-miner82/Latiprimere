#!/usr/bin/env python3
"""Saves what each connection sends to a file, as a printer listening on an AppSocket port (9100) would receive it.

Used on macOS, where Apple's CUPS ignores ServerBin, so the test container's capture backend can't be added. The Raw
queue sends its jobs here through CUPS's own socket backend instead. Each connection becomes
<folder>/<prefix>-<process id>-<number>.prn. It is written under a temporary name and then renamed, so a reader never
sees half a job, and it is readable by everyone.

Usage: python3 capture-server.py <port> <output-folder> [prefix]
"""

import itertools
import os
import socketserver
import sys


class CaptureHandler(socketserver.BaseRequestHandler):
    """Saves one connection's data to a file."""

    def handle(self):
        number = next(self.server.counter)
        name = f"{self.server.prefix}-{os.getpid()}-{number}.prn"
        final_path = os.path.join(self.server.folder, name)
        temporary_path = final_path + ".part"

        # The socket backend sends the job, then shuts down its side and waits for this end to close
        with open(temporary_path, "wb") as output:
            while True:
                chunk = self.request.recv(65536)

                if not chunk:
                    break

                output.write(chunk)

        os.chmod(temporary_path, 0o644)
        os.replace(temporary_path, final_path)
        print(f"Saved {name}", flush=True)


class CaptureServer(socketserver.ThreadingTCPServer):
    """A TCP server on the loopback address that saves each connection to a file."""

    allow_reuse_address = True
    daemon_threads = True

    def __init__(self, port, folder, prefix):
        super().__init__(("127.0.0.1", port), CaptureHandler)
        self.folder = folder
        self.prefix = prefix
        self.counter = itertools.count(1)


def main():
    if len(sys.argv) not in (3, 4):
        print(__doc__, file=sys.stderr)
        return 2

    port = int(sys.argv[1])
    folder = sys.argv[2]
    prefix = sys.argv[3] if len(sys.argv) == 4 else "capture"
    os.makedirs(folder, exist_ok=True)

    with CaptureServer(port, folder, prefix) as server:
        print(f"Capturing connections to 127.0.0.1:{port} in {folder}", flush=True)
        server.serve_forever()

    return 0


if __name__ == "__main__":
    sys.exit(main())
