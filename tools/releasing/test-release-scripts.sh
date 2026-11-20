#!/bin/sh
# Tests switch-to-stable.sh and check-readme-status.sh on scratch copies of the files they change, once with LF and
# once with CRLF line endings, as a Windows checkout has. Both scripts run only at the first stable release, so CI runs
# this on every push (build.yml), and a broken script is found before the release needs it:
#
#   - check-readme-status.sh accepts a preview version and refuses a stable one while the README says preview;
#   - switch-to-stable.sh switches every file's wording, keeps the line prepare-release.sh updates, keeps each file's
#     line endings and changes nothing else, then refuses to run a second time;
#   - check-readme-status.sh then accepts a stable version and refuses a preview.
#
# Once the README says stable, after 1.0, only the last of those is tested, against the repository's own README.
#
# Usage: sh tools/releasing/test-release-scripts.sh, from the repository root. It changes nothing in the repository.
# Exits 1 if any check fails.
set -eu

ROOT=$(pwd)
TOOLS="$ROOT/tools/releasing"
FILES="README.md src/PACKAGE-README.md CHANGELOG.md docs/site/getting-started/installation.md docs/site/getting-started/first-print-job.md"
INSTALL_FILES="README.md src/PACKAGE-README.md docs/site/getting-started/installation.md docs/site/getting-started/first-print-job.md"
FAILURES=0

for file in $FILES; do
	if [ ! -f "$file" ]; then
		echo "Run this from the repository root: $file isn't here." >&2
		exit 1
	fi
done

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

check()
{
	description="$1"
	shift

	if "$@" > /dev/null 2>&1; then
		echo "PASS $description"
	else
		echo "FAIL $description" >&2
		FAILURES=$((FAILURES + 1))
	fi
}

# Runs a command in the scratch copy
in_copy()
{
	(cd "$COPY" && "$@")
}

refuses()
{
	! in_copy "$@"
}

# Prints a file of the copy without carriage returns
plain()
{
	tr -d '\r' < "$COPY/$1"
}

has_line()
{
	plain "$1" | grep -Eq "$2"
}

lacks_line()
{
	! plain "$1" | grep -Eq "$2"
}

# Whether every line of a file has the given endings: as many carriage returns as lines for crlf, none for lf. They
# are counted with tr, as Git Bash's grep doesn't see them
has_endings()
{
	returns=$(tr -cd '\r' < "$COPY/$1" | wc -c | tr -d ' ')
	lines=$(wc -l < "$COPY/$1" | tr -d ' ')

	if [ "$2" = crlf ]; then
		[ "$returns" = "$lines" ]
	else
		[ "$returns" = 0 ]
	fi
}

# Whether the switch changed no more lines than it should: lines that differ between the original and the copy
changed_lines()
{
	tr -d '\r' < "$ROOT/$1" > "$WORK/before"
	plain "$1" > "$WORK/after"
	diff "$WORK/before" "$WORK/after" | grep -c '^>' || true
}

at_most_lines_changed()
{
	[ "$(changed_lines "$1")" -le "$2" ]
}

# After 1.0 the switch has been made, and can't be made again: only the status check is left to test
if tr -d '\r' < README.md | grep -Eq '^> \*\*Status:\*\* stable\.'; then
	COPY="$ROOT"
	echo "== The README already says stable, so only check-readme-status.sh is tested"
	check "check-readme-status.sh accepts a stable version" in_copy sh "$TOOLS/check-readme-status.sh" 11.0.99
	check "check-readme-status.sh refuses a preview" refuses sh "$TOOLS/check-readme-status.sh" 11.0.99-preview

	if [ "$FAILURES" -gt 0 ]; then
		echo "$FAILURES check(s) failed." >&2
		exit 1
	fi

	echo "Every check passed."
	exit 0
fi

for endings in lf crlf; do
	COPY="$WORK/$endings"

	for file in $FILES; do
		mkdir -p "$COPY/$(dirname "$file")"

		if [ "$endings" = crlf ]; then
			tr -d '\r' < "$file" | awk '{ printf "%s\r\n", $0 }' > "$COPY/$file"
		else
			tr -d '\r' < "$file" > "$COPY/$file"
		fi
	done

	echo "== $endings"

	check "the README says preview" has_line README.md '^> \*\*Status:\*\* preview\.'
	check "check-readme-status.sh accepts a preview" in_copy sh "$TOOLS/check-readme-status.sh" 11.0.99-preview
	check "check-readme-status.sh refuses a stable version" refuses sh "$TOOLS/check-readme-status.sh" 11.0.99

	check "switch-to-stable.sh runs" in_copy sh "$TOOLS/switch-to-stable.sh"

	check "the README says stable" has_line README.md '^> \*\*Status:\*\* stable\. Version [^ ]+ is on \[NuGet\]'
	check "prepare-release.sh can still set the README's version" has_line README.md '^> \*\*Status:\*\*.*[Vv]ersion [^ ]+ is on'
	check "the package README says stable" has_line src/PACKAGE-README.md '^> \*\*Stable\.\*\*'
	check "the package README no longer says preview" lacks_line src/PACKAGE-README.md '^> \*\*Preview\.\*\*'
	check "the installation page no longer says only previews are published" lacks_line docs/site/getting-started/installation.md '^Only previews are published'

	for file in $INSTALL_FILES; do
		check "$file installs without --prerelease" lacks_line "$file" '^dotnet add package .*--prerelease'
		check "$file still installs Latiprimere" has_line "$file" '^dotnet add package Latiprimere( |$)'
	done

	check "the changelog's notes start with what 1.0 means" sh -c "tr -d '\r' < '$COPY/CHANGELOG.md' | grep -A 2 -x '## Unreleased' | grep -q '^\*\*The first stable release: Latiprimere 1.0\.\*\*'"

	for file in $FILES; do
		check "$file keeps its $endings line endings" has_endings "$file" "$endings"
	done

	check "the README's status is the only line changed there" at_most_lines_changed README.md 5
	check "the changelog gains only the 1.0 paragraph" at_most_lines_changed CHANGELOG.md 2
	check "the package README changes only its status and install lines" at_most_lines_changed src/PACKAGE-README.md 5

	check "switch-to-stable.sh refuses a second run" refuses sh "$TOOLS/switch-to-stable.sh"
	check "check-readme-status.sh then accepts a stable version" in_copy sh "$TOOLS/check-readme-status.sh" 11.0.99
	check "check-readme-status.sh then refuses a preview" refuses sh "$TOOLS/check-readme-status.sh" 11.0.99-preview
done

if [ "$FAILURES" -gt 0 ]; then
	echo "$FAILURES check(s) failed." >&2
	exit 1
fi

echo "Every check passed."
