# CUPS print server with virtual printers, for Latiprimere development and integration testing.
# See README.md in this folder.
FROM debian:bookworm-slim

# CUPS 2.4, the IPP tools (ippeveprinter, ipptool), CUPS-PDF, and ICU so self-contained .NET apps can run inside.
# ippeveprinter in CUPS 2.4 will not start without DNS-SD, so D-Bus and Avahi are included too.
RUN apt-get update \
	&& DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
		cups \
		cups-ipp-utils \
		printer-driver-cups-pdf \
		avahi-daemon \
		dbus \
		libicu72 \
	&& rm -rf /var/lib/apt/lists/*

# Server configuration: listen on all interfaces, allow file: devices for the raw queues, write PDFs to /output
COPY cupsd.conf /etc/cups/cupsd.conf
RUN echo "FileDevice Yes" >> /etc/cups/cups-files.conf \
	&& sed -i -E 's|^#?Out .*|Out /output/pdf|; s|^#?AnonDirName .*|AnonDirName /output/pdf|' /etc/cups/cups-pdf.conf

# Test user for the Secure queue, which needs a user name and password. CUPS checks it through PAM against the
# container's own accounts. A throwaway account in a container published on 127.0.0.1 only; the integration tests
# use the same name and password (CupsAuthenticationIntegrationTests).
RUN useradd --no-create-home --shell /usr/sbin/nologin latiprimere \
	&& echo "latiprimere:test-password" | chpasswd

# "capture" backend: saves raw job data to /output/<queue>-<job-id>.prn
COPY capture-backend.sh /usr/lib/cups/backend/capture
RUN chmod 0755 /usr/lib/cups/backend/capture

COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

# 631: CUPS. 8631: the virtual IPP Everywhere printer, for direct IPP testing.
EXPOSE 631 8631

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
