#!/bin/sh
# Makes the wording changes of the first stable release, 1.0 (docs/releasing.md, "The first stable release"):
#
#   - README.md's status line says stable, keeping "Version <version> is on", which prepare-release.sh updates;
#   - src/PACKAGE-README.md, which nuget.org shows, says stable instead of preview;
#   - "dotnet add package" commands lose --prerelease, in README.md, src/PACKAGE-README.md and the getting started
#     pages, and the installation page no longer says only previews are published;
#   - CHANGELOG.md's notes under "## Unreleased" start with what 1.0 means, adding the heading if there isn't one.
#
# Run it only once the real-printer sign-off is done, on a branch from main, then set the repository variable
# RELEASE_PREVIEW to false before the release: prepare-release.sh refuses a preview version while README.md says
# stable, and a stable version while it says preview.
#
# Usage: sh tools/releasing/switch-to-stable.sh, from the repository root (Linux, macOS, or Git Bash on Windows). It
# only changes files: committing them is up to whoever runs it. Each file keeps its line endings.
set -eu

README=README.md
PACKAGE_README=src/PACKAGE-README.md
INSTALLATION=docs/site/getting-started/installation.md
FIRST_PRINT_JOB=docs/site/getting-started/first-print-job.md
CHANGELOG=CHANGELOG.md

for file in "$README" "$PACKAGE_README" "$INSTALLATION" "$FIRST_PRINT_JOB" "$CHANGELOG"; do
	if [ ! -f "$file" ]; then
		echo "Run this from the repository root: $file isn't here." >&2
		exit 1
	fi
done

# Writes standard input to a file, with CRLF line endings if asked
write_file()
{
	if [ "$2" = "crlf" ]; then
		awk '{ printf "%s\r\n", $0 }' > "$1.tmp"
	else
		cat > "$1.tmp"
	fi

	mv "$1.tmp" "$1"
}

# Prints "crlf" if a file has CRLF line endings, as in a Windows checkout, and "lf" otherwise
line_endings()
{
	# shellcheck disable=SC2094 # Both sides only read the file
	if tr -d '\r' < "$1" | cmp -s - "$1"; then
		echo lf
	else
		echo crlf
	fi
}

# Prints a file without carriage returns
read_file()
{
	tr -d '\r' < "$1"
}

# Runs a sed script over a file, keeping its line endings
edit_file()
{
	endings=$(line_endings "$1")
	read_file "$1" | sed -E "$2" | write_file "$1" "$endings"
}

STATUS_PATTERN='^> \*\*Status:\*\* preview\. Version [^ ]+ is on \[NuGet\]\([^)]*\)'

if ! read_file "$README" | grep -Eq "$STATUS_PATTERN"; then
	if read_file "$README" | grep -Eq '^> \*\*Status:\*\* stable\.'; then
		echo "$README already says stable: nothing to do." >&2
	else
		echo "$README has no '> **Status:** preview. Version <version> is on [NuGet](...)' line to change." >&2
	fi

	exit 1
fi

if ! read_file "$PACKAGE_README" | grep -q '^> \*\*Preview\.\*\*'; then
	echo "$PACKAGE_README has no '> **Preview.**' line to change." >&2
	exit 1
fi

if ! read_file "$INSTALLATION" | grep -q '^Only previews are published so far'; then
	echo "$INSTALLATION has no 'Only previews are published so far' line to change." >&2
	exit 1
fi

# README.md: the version stays, for prepare-release.sh to set to the release's
edit_file "$README" \
	"s|^> \*\*Status:\*\* preview\. Version ([^ ]+) is on \[NuGet\]\(([^)]*)\).*|> **Status:** stable. Version \1 is on [NuGet](\2). From 1.0, the public API changes only by additions until the next .NET major version: see [Changing the Public API](CONTRIBUTING.md#changing-the-public-api).|"

# src/PACKAGE-README.md, which nuget.org shows: no links, as nuget.org doesn't resolve relative ones
edit_file "$PACKAGE_README" \
	's|^> \*\*Preview\.\*\*.*|> **Stable.** From 1.0, the public API changes only by additions until the next .NET major version.|'

# Install commands: stable versions need no --prerelease. Each line loses the same text, so aligned comments stay aligned
for file in "$README" "$PACKAGE_README" "$INSTALLATION" "$FIRST_PRINT_JOB"; do
	edit_file "$file" 's#^(dotnet add package [^ ]+) --prerelease#\1#'
done

# shellcheck disable=SC2016 # The backticks are Markdown, not a command
edit_file "$INSTALLATION" 's#^Only previews are published so far, so add `--prerelease`:$#Add the ones you need:#'

# CHANGELOG.md: what 1.0 means, first under "## Unreleased", which prepare-release.sh dates
NOTES='**The first stable release: Latiprimere 1.0.** The packages keep their version numbers after .NET, as 11.0.<n>, and "1.0" names this first stable release ([ADR 0030](docs/decisions/0030-release-numbering.md), and decision D8 of the [Phase 3 plan](docs/phase-3-plan.md)). From here the public API changes only by additions until the next .NET major version, by the rules in [CONTRIBUTING.md](CONTRIBUTING.md#changing-the-public-api) ([ADR 0044](docs/decisions/0044-room-to-grow-after-1-0.md)). Before release it was checked on paper on a real printer, through the Windows spooler and over IPP and IPPS, and on a Mac. Coming from a preview: the names that changed for 1.0 are in 11.0.7-preview'"'"'s notes.'

endings=$(line_endings "$CHANGELOG")

read_file "$CHANGELOG" \
	| awk -v notes="$NOTES" '
		# Under the first "## Unreleased", or a new one before the first release heading
		!done && $0 == "## Unreleased" { print; print ""; print notes; done = 1; next }
		!done && /^## / { print "## Unreleased"; print ""; print notes; print ""; done = 1 }
		{ print }
	' \
	| write_file "$CHANGELOG" "$endings"

echo "Switched to stable wording. Review the changes, then set RELEASE_PREVIEW to false before the release."
