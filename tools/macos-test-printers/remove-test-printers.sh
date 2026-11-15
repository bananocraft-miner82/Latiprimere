#!/bin/sh
# Undoes install-test-printers.sh: removes the test queues, stops the capture server and ippeveprinter, and turns
# CUPS's debug logging off. The output folder and Homebrew's cups, if it was installed, are left in place.
#
# Usage: sh tools/macos-test-printers/remove-test-printers.sh
set -eu

STATE_DIRECTORY=/tmp/latiprimere-test-printers
CUPS_FILES_CONF=/etc/cups/cups-files.conf
CUPS_FILES_BACKUP="$CUPS_FILES_CONF.latiprimere-backup" # Left by an earlier version of the install script

for queue in Raw Paused Virtual-IPP; do
	sudo lpadmin -x "$queue" 2> /dev/null || true
done

for pid_file in "$STATE_DIRECTORY"/*.pid; do
	if [ -f "$pid_file" ]; then
		kill "$(cat "$pid_file")" 2> /dev/null || true
	fi
done

rm -rf "$STATE_DIRECTORY"

# Earlier versions of the install script pointed ServerBin at a copy of /usr/libexec/cups, which Apple's CUPS ignores
if [ -f "$CUPS_FILES_BACKUP" ]; then
	sudo mv "$CUPS_FILES_BACKUP" "$CUPS_FILES_CONF"
fi

sudo rm -rf /Library/Printers/Latiprimere /opt/latiprimere
sudo cupsctl --no-debug-logging || true

echo "Latiprimere test printers removed."
