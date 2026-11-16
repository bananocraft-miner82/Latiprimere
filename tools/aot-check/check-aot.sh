#!/usr/bin/env bash
# Publishes the CLI sample with Native AOT, fails on any trimming or AOT warning, then runs the native program
# against whichever test printers this machine has:
#
#   - always: listing printers;
#   - LATIPRIMERE_TEST_CUPS_SERVER set: a PDF to the Virtual-IPP queue, with colour and vendor options;
#   - LATIPRIMERE_TEST_IPP_PRINTER set: a PDF straight to that printer with paper, tray, colour and vendor
#     options, and a vendor value it must refuse;
#   - Windows with Microsoft Print to PDF: a PDF on A5 to a file, whose pages must be A5.
#
# Usage: tools/aot-check/check-aot.sh [target-framework]   (default net10.0)
# Needs the platform's native toolchain: Visual Studio's C++ tools on Windows, clang and zlib on Linux, Xcode on
# macOS. Run from the repository root.

set -euo pipefail

framework="${1:-net10.0}"
output_directory="out/aot-check/$framework"
document="tools/test-documents/three-pages.pdf"

# The ILCompiler finds the Windows linker through vswhere, which Git Bash may not have on its path
if [[ "${OS:-}" == "Windows_NT" && -d "/c/Program Files (x86)/Microsoft Visual Studio/Installer" ]]; then
	PATH="/c/Program Files (x86)/Microsoft Visual Studio/Installer:$PATH"
fi

echo "== Publishing samples/Latiprimere.Cli for $framework with Native AOT"
rm -rf "$output_directory"
publish_log="$(mktemp)"

# Each trimming and AOT warning on its own (not one per assembly), so the log says what to fix
if ! dotnet publish samples/Latiprimere.Cli --configuration Release --framework "$framework" --use-current-runtime \
	-p:PublishAot=true -p:TrimmerSingleWarn=false --output "$output_directory" -nologo | tee "$publish_log"; then
	echo "Native AOT publish failed." >&2
	exit 1
fi

# IL2xxx are trimming warnings, IL3xxx AOT ones; either means some code may not work once published
if grep -E "warning IL[23][0-9]{3}" "$publish_log"; then
	echo "Native AOT publish reported trimming or AOT warnings." >&2
	exit 1
fi

cli="$output_directory/Latiprimere.Cli"
[[ -f "$cli.exe" ]] && cli="$cli.exe"
ls -l "$output_directory"

# Runs the native CLI and checks its exit code
run() {
	local expected="$1"
	shift
	echo "== Latiprimere.Cli $*"
	local status=0
	"$cli" "$@" || status=$?

	if [[ "$status" != "$expected" ]]; then
		echo "Expected exit code $expected, got $status." >&2
		exit 1
	fi
}

run 0 list

if [[ -n "${LATIPRIMERE_TEST_CUPS_SERVER:-}" ]]; then
	export CUPS_SERVER="$LATIPRIMERE_TEST_CUPS_SERVER"
	run 0 print cups:Virtual-IPP "$document" --color mono --option ipp:print-quality=4 --option cups:fit-to-page=true
fi

if [[ -n "${LATIPRIMERE_TEST_IPP_PRINTER:-}" ]]; then
	export LATIPRIMERE_IPP_PRINTERS="$LATIPRIMERE_TEST_IPP_PRINTER"
	printer="ipp:$LATIPRIMERE_TEST_IPP_PRINTER"
	run 0 caps "$printer"

	# ippeveprinter lists a manual tray; a build without one is still checked without --tray. The odd expansion keeps
	# an empty array working under set -u in bash 3.2, which macOS has
	tray_arguments=()
	if "$cli" caps "$printer" | grep "(manual)" > /dev/null; then
		tray_arguments=(--tray manual)
	fi

	run 0 print "$printer" "$document" --paper iso_a4_210x297mm ${tray_arguments[@]+"${tray_arguments[@]}"} --color mono \
		--option ipp:print-quality=5 --option ipp:ipp-attribute-fidelity=true

	# ippeveprinter supports print-quality 3 to 5, so this shows the attribute reaches it
	run 1 print "$printer" "$document" --option ipp:print-quality=99
fi

if [[ "${OS:-}" == "Windows_NT" ]] && "$cli" list | grep "(windows:Microsoft Print to PDF," > /dev/null; then
	pdf_file="$(cygpath -w "$PWD/$output_directory/a5.pdf")"
	run 0 print "windows:Microsoft Print to PDF" "$document" --paper iso_a5_148x210mm --color mono --output "$pdf_file"

	# The driver writes each page's MediaBox in points; A5 is 419.5 x 595.3
	if ! grep -a -q "/MediaBox \[ 0.0 0.0 419.5" "$output_directory/a5.pdf"; then
		echo "Microsoft Print to PDF didn't write A5 pages." >&2
		grep -a -o "/MediaBox \[[^]]*\]" "$output_directory/a5.pdf" >&2 || true
		exit 1
	fi
fi

echo "== Native AOT check passed for $framework"
