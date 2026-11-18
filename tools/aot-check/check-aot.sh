#!/usr/bin/env bash
# Publishes the CLI sample and the end-to-end harness with Native AOT, fails on any trimming or AOT warning, then runs
# the native harness's scenarios against whichever test printers this machine has:
#
#   - catalog.list-across-print-systems: listing every print system's printers, always;
#   - cups.submission.vendor-options: a PDF to the Virtual-IPP queue with monochrome and vendor options, with
#     LATIPRIMERE_TEST_CUPS_SERVER set;
#   - ipp.submission.virtual-printer-options: a PDF straight to that printer with paper, tray, colour and vendor
#     options, and a vendor value it must refuse, with LATIPRIMERE_TEST_IPP_PRINTER set;
#   - windows.output.paper-and-orientation: Microsoft Print to PDF on A4, A5 and landscape, on Windows with that
#     printer, its pages measured;
#   - windows.output.print-ticket: the same printer on A5 from a Print Ticket, which goes through prntvpt.dll by
#     function pointers into COM, so it is worth checking natively (ADR 0031);
#   - windows.vendor-options.listed-and-sent: that printer's Print Capabilities read, parsed and printed with
#     (ADR 0032);
#   - ipp.vendor-options.listed-and-sent: vendor options read from raw IPP attributes and sent back, against the
#     scripted IPP printer, so on every runner (ADR 0032).
#
# A scenario whose test printers this machine doesn't have is skipped; one that runs must pass. The first four are
# the checks this script made with the CLI sample before the harness existed: CI runs only these (ADR 0029). Run every scenario
# natively with tools/test-harness/run.ps1 -Aot or run.sh --aot.
#
# Usage: tools/aot-check/check-aot.sh [target-framework]   (default net10.0)
# Needs the platform's native toolchain: Visual Studio's C++ tools on Windows, clang and zlib on Linux, Xcode on
# macOS. Run from the repository root.

set -euo pipefail

framework="${1:-net10.0}"
output_directory="out/aot-check/$framework"
harness_directory="out/aot-check/$framework-harness"
scenarios=(
	catalog.list-across-print-systems
	cups.submission.vendor-options
	ipp.submission.virtual-printer-options
	windows.output.paper-and-orientation
	windows.output.print-ticket
	windows.vendor-options.listed-and-sent
	ipp.vendor-options.listed-and-sent
)

# The ILCompiler finds the Windows linker through vswhere, which Git Bash may not have on its path
if [[ "${OS:-}" == "Windows_NT" && -d "/c/Program Files (x86)/Microsoft Visual Studio/Installer" ]]; then
	PATH="/c/Program Files (x86)/Microsoft Visual Studio/Installer:$PATH"
fi

# Publishes a project with Native AOT, failing on any trimming or AOT warning
publish() {
	local project="$1"
	local output="$2"
	echo "== Publishing $project for $framework with Native AOT"
	rm -rf "$output"
	local publish_log
	publish_log="$(mktemp)"

	# Each trimming and AOT warning on its own (not one per assembly), so the log says what to fix
	if ! dotnet publish "$project" --configuration Release --framework "$framework" --use-current-runtime \
		-p:PublishAot=true -p:TrimmerSingleWarn=false --output "$output" -nologo | tee "$publish_log"; then
		echo "Native AOT publish of $project failed." >&2
		exit 1
	fi

	# IL2xxx are trimming warnings, IL3xxx AOT ones; either means some code may not work once published
	if grep -E "warning IL[23][0-9]{3}" "$publish_log"; then
		echo "Native AOT publish of $project reported trimming or AOT warnings." >&2
		exit 1
	fi
}

# The CLI sample stands for an application using every package
publish samples/Latiprimere.Cli "$output_directory"
ls -l "$output_directory"

publish tests/Latiprimere.TestHarness "$harness_directory"

harness="$harness_directory/Latiprimere.TestHarness"
[[ -f "$harness.exe" ]] && harness="$harness.exe"

# On macOS, launchd starts CUPS on demand, and it exits after about a minute with nothing to do. Only its local
# socket starts it again, not the TCP port Latiprimere connects to, and the publishes above leave it idle for
# minutes. lpstat goes through that socket, so it wakes the scheduler; then wait for the port to answer.
wake_macos_cups() {
	local server="$LATIPRIMERE_TEST_CUPS_SERVER"
	local host="${server%:*}"
	local port="${server##*:}"
	[[ "$server" == *:* ]] || port=631
	local attempts=0

	until lpstat -r 2> /dev/null | grep -q "scheduler is running" && nc -z "$host" "$port" 2> /dev/null; do
		attempts=$((attempts + 1))

		if [[ "$attempts" -ge 60 ]]; then
			echo "The CUPS scheduler did not answer on $host:$port." >&2
			exit 1
		fi

		sleep 0.5
	done
}

if [[ "$(uname -s)" == "Darwin" && -n "${LATIPRIMERE_TEST_CUPS_SERVER:-}" ]]; then
	wake_macos_cups
fi

echo "== Running the native harness's scenarios"
"$harness" targets
"$harness" run "${scenarios[@]}"

echo "== Native AOT check passed for $framework"
