#!/bin/sh
# CUPS backend that saves each job's data unchanged to <folder>/<queue>-<job-id>.prn.
# Used for the Raw, Paused and Secure queues, so tests can check exactly which bytes reached the "printer".
# The device URI names the folder: capture:/ means /output, as in the test container; capture:/some/folder means
# /some/folder, as on a Mac set up by tools/macos-test-printers.
# CUPS calls backends as: backend job-id user title copies options [file]

# With no arguments, CUPS is asking which devices this backend provides
if [ $# -eq 0 ]; then
	echo 'direct capture "Unknown" "Capture job data to a file"'
	exit 0
fi

JOB_ID="$1"
INPUT="${6:-/dev/stdin}"

OUTPUT_DIRECTORY="${DEVICE_URI#capture:}"
if [ -z "$OUTPUT_DIRECTORY" ] || [ "$OUTPUT_DIRECTORY" = "/" ]; then
	OUTPUT_DIRECTORY=/output
fi

# CUPS runs backends as lp with umask 077, which would leave the file readable by lp alone. On a Linux host the
# output folder is a bind mount, and the tests, running as another user, could not read it. A file left by an
# earlier container (job IDs start again) is replaced rather than overwritten, which would keep its old mode.
umask 022
OUTPUT_FILE="${OUTPUT_DIRECTORY}/${PRINTER:-capture}-${JOB_ID}.prn"
rm -f "$OUTPUT_FILE"
cat "$INPUT" > "$OUTPUT_FILE"
