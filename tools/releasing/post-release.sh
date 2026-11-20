#!/bin/sh
# Makes the changes of the post-release commit, which follows the release commit on post-release/v<version>
# (docs/releasing.md, ADR 0040):
#
#   - has packing compare the packages with the release just published, rather than the one before it, which the
#     release commit compared them with (PackageValidationBaselineVersion in src/Directory.Build.props);
#   - removes every src/*/CompatibilitySuppressions.xml. They excused this release's breaking changes, which are in
#     the new baseline, and package validation fails on a suppression it doesn't need.
#
# So main checks a change against what is on nuget.org now. Comparing it with the release before would pass a change
# to something this release first shipped, and the next release's packing would fail on it.
#
# Usage: sh tools/releasing/post-release.sh <version>, for example 11.0.42, from the repository root. The release
# workflow runs it once the packages are on nuget.org. It only changes files: committing them is up to whoever runs
# it. Each file keeps its line endings.
set -eu

VERSION="${1:-}"

if ! printf '%s\n' "$VERSION" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$'; then
	echo "Give the version just released, such as 11.0.42: '$VERSION' isn't one." >&2
	exit 1
fi

PROPS=src/Directory.Build.props

if [ ! -f "$PROPS" ]; then
	echo "Run this from the repository root." >&2
	exit 1
fi

if ! tr -d '\r' < "$PROPS" | grep -q '<PackageValidationBaselineVersion>.*</PackageValidationBaselineVersion>'; then
	echo "$PROPS has no <PackageValidationBaselineVersion> property to set." >&2
	exit 1
fi

# CRLF in a Windows checkout, LF otherwise; Git Bash's grep doesn't see carriage returns, so compare instead
# shellcheck disable=SC2094 # Both sides only read the file
if tr -d '\r' < "$PROPS" | cmp -s - "$PROPS"; then
	tr -d '\r' < "$PROPS" \
		| sed "s|<PackageValidationBaselineVersion>[^<]*</PackageValidationBaselineVersion>|<PackageValidationBaselineVersion>$VERSION</PackageValidationBaselineVersion>|" \
		> "$PROPS.tmp"
else
	tr -d '\r' < "$PROPS" \
		| sed "s|<PackageValidationBaselineVersion>[^<]*</PackageValidationBaselineVersion>|<PackageValidationBaselineVersion>$VERSION</PackageValidationBaselineVersion>|" \
		| awk '{ printf "%s\r\n", $0 }' \
		> "$PROPS.tmp"
fi

mv "$PROPS.tmp" "$PROPS"
echo "Packages are compared with $VERSION."

for suppressions in src/*/CompatibilitySuppressions.xml; do
	[ -f "$suppressions" ] || continue

	rm "$suppressions"
	echo "$(basename "$(dirname "$suppressions")"): compatibility suppressions removed, as $VERSION has their changes"
done
