#!/bin/sh
# Starts the virtual IPP printer and CUPS, then creates the test queues.
set -e

mkdir -p /output/pdf /var/spool/ippeve /run/dbus
chmod 777 /output /output/pdf

# Files left by the previous run when the container restarts, as it does after the host restarts: a stale CUPS
# socket would make the wait below pass before cupsd is listening, and lpadmin would then fail
rm -f /run/dbus/pid /run/avahi-daemon/pid /run/cups/cups.sock

# D-Bus and Avahi, which ippeveprinter needs for DNS-SD
dbus-daemon --system --fork
avahi-daemon --daemonize --no-drop-root --no-chroot
until avahi-daemon --check; do sleep 0.5; done

# Virtual IPP Everywhere printer on port 8631. Keeps received documents in /var/spool/ippeve.
# "-c /bin/true" finishes each job at once: without a print command, ippeveprinter pretends to print for a random
# 5-15 s, and since it takes one job at a time, parallel test runs queued behind each other for most of a minute.
ippeveprinter -p 8631 -r off -k -d /var/spool/ippeve -c /bin/true \
	-f application/pdf,image/pwg-raster,image/jpeg \
	-M Latiprimere -m "Virtual IPP Printer" \
	"Virtual IPP Printer" &

# CUPS scheduler in the foreground of a background job, so this script can configure it and then wait
cupsd -f &

# Wait for both servers to answer
until lpstat -r 2>/dev/null | grep -q "is running"; do sleep 0.5; done
until ipptool -q ipp://localhost:8631/ipp/print get-printer-attributes.test; do sleep 0.5; done

# CUPS-PDF: renders jobs to PDF files in /output/pdf
CUPS_PDF_PPD=$(find /usr/share/ppd -name 'CUPS-PDF_opt.ppd' | head -n 1)
lpadmin -p CUPS-PDF -E -v cups-pdf:/ -P "$CUPS_PDF_PPD" -D "Writes PDF files" -L "Test container"

# Raw: saves job data unchanged to /output/Raw-<job-id>.prn (file: devices write nothing for unfiltered raw jobs)
lpadmin -p Raw -E -v capture:/ -D "Writes raw data to a file" -L "Test container"

# Paused: a raw queue that is disabled, so jobs stay pending until cancelled
lpadmin -p Paused -E -v capture:/ -D "Disabled queue for cancellation tests" -L "Test container"
cupsdisable -r "Paused for testing" Paused

# Secure: a raw queue that needs a user name and password over an encrypted connection to print (see cupsd.conf)
lpadmin -p Secure -E -v capture:/ -D "Needs a user name and password" -L "Test container" \
	-o printer-op-policy=authenticated

# Virtual-IPP: driverless IPP Everywhere queue pointing at the virtual printer
lpadmin -p Virtual-IPP -E -v ipp://localhost:8631/ipp/print -m everywhere -D "Virtual IPP Everywhere printer" -L "Test container"

lpadmin -d CUPS-PDF
echo "Latiprimere virtual printers ready: $(lpstat -e | tr '\n' ' ')"

wait
