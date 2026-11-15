#!/bin/sh
# Creates Latiprimere's test printers on this Mac's own CUPS, like the test container in tools/virtual-printers
# does on Linux. Nothing prints on paper.
#
#   Raw          Raw queue that sends each job, unchanged, to the capture server on 127.0.0.1:9101, which saves
#                it as <output-folder>/capture-<process id>-<number>.prn
#   Paused       Same, but the queue is disabled, so jobs stay pending until cancelled
#   Virtual-IPP  Driverless IPP Everywhere queue in front of ippeveprinter
#   ippeveprinter on port 8631, for direct IPP tests
#
# Apple's CUPS ignores ServerBin, so on macOS no backend can be added, and the container's capture backend can't be
# used. The Raw queue uses CUPS's own socket backend (AppSocket, as for port 9100 printers) instead, with
# capture-server.py in place of a printer. Its files are named by the server, not by CUPS job ID, so the tests find
# a job's file by its content (LATIPRIMERE_TEST_CUPS_SETUP=macos).
#
# Meant for CI runners and spare Macs: it adds queues to the system CUPS and turns on its debug logging. Run
# remove-test-printers.sh to undo. Re-running this script recreates everything.
#
# Usage: sh tools/macos-test-printers/install-test-printers.sh [output-folder]
# Run it as yourself, not with sudo: it asks sudo for the steps that need root, and the servers run as you.
set -eu

SCRIPT_DIRECTORY=$(cd "$(dirname "$0")" && pwd)
OUTPUT_DIRECTORY="${1:-/Users/Shared/LatiprimereTestOutput}"
STATE_DIRECTORY=/tmp/latiprimere-test-printers # Logs and process IDs of both servers, and ippeveprinter's spool
IPPEVE_PORT=8631
CAPTURE_PORT=9101

# Waits until the CUPS scheduler answers, starting it if launchd hasn't yet
wait_for_scheduler()
{
	attempts=0

	until lpstat -r 2> /dev/null | grep -q "scheduler is running"; do
		attempts=$((attempts + 1))

		if [ "$attempts" -ge 60 ]; then
			echo "The CUPS scheduler did not start." >&2
			exit 1
		fi

		sleep 0.5
	done
}

# Waits until a server accepts connections on a port, or shows its log and stops.
# Arguments: port, name, log file.
wait_for_port()
{
	attempts=0

	until nc -z 127.0.0.1 "$1" 2> /dev/null; do
		attempts=$((attempts + 1))

		if [ "$attempts" -ge 60 ]; then
			echo "$2 did not start on port $1:" >&2
			cat "$3" >&2
			exit 1
		fi

		sleep 0.5
	done
}

if ! command -v python3 > /dev/null; then
	echo "The capture server needs python3." >&2
	exit 1
fi

# ippeveprinter: macOS may not include it, but Homebrew's cups (keg-only, beside the system CUPS) does
IPPEVEPRINTER=$(command -v ippeveprinter || true)

if [ -z "$IPPEVEPRINTER" ]; then
	if ! command -v brew > /dev/null; then
		echo "ippeveprinter is missing. Install Homebrew, then run this script again: it installs Homebrew's cups." >&2
		exit 1
	fi

	brew list cups > /dev/null 2>&1 || HOMEBREW_NO_AUTO_UPDATE=1 HOMEBREW_NO_INSTALL_CLEANUP=1 brew install cups
	IPPEVEPRINTER="$(brew --prefix cups)/bin/ippeveprinter"
fi

# Stop the servers of an earlier run
for pid_file in "$STATE_DIRECTORY"/*.pid; do
	if [ -f "$pid_file" ]; then
		kill "$(cat "$pid_file")" 2> /dev/null || true
	fi
done

rm -rf "$STATE_DIRECTORY"
mkdir -p "$STATE_DIRECTORY/spool" "$OUTPUT_DIRECTORY"

# The capture server, in place of a printer for the Raw and Paused queues
nohup python3 "$SCRIPT_DIRECTORY/capture-server.py" "$CAPTURE_PORT" "$OUTPUT_DIRECTORY" capture \
	> "$STATE_DIRECTORY/capture-server.log" 2>&1 &
echo $! > "$STATE_DIRECTORY/capture-server.pid"
wait_for_port "$CAPTURE_PORT" "The capture server" "$STATE_DIRECTORY/capture-server.log"

# The virtual IPP printer, with the same options as the container: "-c /usr/bin/true" finishes each job at once,
# instead of pretending to print for 5-15 s while turning other jobs away busy
nohup "$IPPEVEPRINTER" -p "$IPPEVE_PORT" -r off -k -d "$STATE_DIRECTORY/spool" -c /usr/bin/true \
	-f application/pdf,image/pwg-raster,image/jpeg \
	-M Latiprimere -m "Virtual IPP Printer" \
	"Virtual IPP Printer" > "$STATE_DIRECTORY/ippeveprinter.log" 2>&1 &
echo $! > "$STATE_DIRECTORY/ippeveprinter.pid"
wait_for_port "$IPPEVE_PORT" "ippeveprinter ($IPPEVEPRINTER)" "$STATE_DIRECTORY/ippeveprinter.log"

# Debug logging, so a failed CI run shows why in /var/log/cups/error_log
wait_for_scheduler
sudo cupsctl --debug-logging
wait_for_scheduler

# The queues, as in tools/virtual-printers/entrypoint.sh. "snmp=false" stops the socket backend asking the
# "printer" for supply levels over SNMP, which nothing answers.
for queue in Raw Paused Virtual-IPP; do
	sudo lpadmin -x "$queue" 2> /dev/null || true
done

CAPTURE_URI="socket://127.0.0.1:$CAPTURE_PORT/?snmp=false"
sudo lpadmin -p Raw -E -v "$CAPTURE_URI" -D "Sends raw data to the capture server" -L "Latiprimere test printers"
sudo lpadmin -p Paused -E -v "$CAPTURE_URI" -D "Disabled queue for cancellation tests" -L "Latiprimere test printers"
sudo cupsdisable -r "Paused for testing" Paused
sudo lpadmin -p Virtual-IPP -E -v "ipp://localhost:$IPPEVE_PORT/ipp/print" -m everywhere \
	-D "Virtual IPP Everywhere printer" -L "Latiprimere test printers"

echo "Latiprimere test printers ready (ippeveprinter: $IPPEVEPRINTER, output: $OUTPUT_DIRECTORY):"
lpstat -v Raw Paused Virtual-IPP
