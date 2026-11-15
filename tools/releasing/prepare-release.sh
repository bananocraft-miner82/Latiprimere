#!/bin/sh
# Makes the changes of a release commit (docs/releasing.md):
#
#   - sets the version in Directory.Build.props;
#   - marks the public API as shipped: moves each src/*/PublicAPI.Unshipped.txt into PublicAPI.Shipped.txt, and
#     drops the shipped lines that *REMOVED* entries name;
#   - has packing compare the packages with the previous release, if there was one
#     (PackageValidationBaselineVersion in src/Directory.Build.props);
#   - updates the status line in README.md.
#
# Usage: sh tools/releasing/prepare-release.sh <version>, for example 0.1.0-preview, from the repository root.
# The prepare-release workflow runs it; it works locally too (Linux, macOS, or Git Bash on Windows). It only
# changes files: committing them is up to whoever runs it. Each file keeps its line endings.
set -eu

VERSION="${1:-}"

if ! printf '%s\n' "$VERSION" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$'; then
	echo "Give the version to release, such as 0.1.0-preview or 1.0.0: '$VERSION' isn't one." >&2
	exit 1
fi

if [ ! -f Directory.Build.props ] || [ ! -f src/Directory.Build.props ] || [ ! -f README.md ]; then
	echo "Run this from the repository root." >&2
	exit 1
fi

if git rev-parse -q --verify "refs/tags/v$VERSION" > /dev/null; then
	echo "v$VERSION is already tagged, and a published version can't be published again." >&2
	exit 1
fi

VERSION_PREFIX="${VERSION%%-*}"
VERSION_SUFFIX=""

case "$VERSION" in
	*-*) VERSION_SUFFIX="${VERSION#*-}" ;;
esac

# The last release before this commit, if any: the newest v* tag it contains
PREVIOUS_TAG=$(git tag --merged HEAD --list 'v*' --sort=-creatordate | head -n 1)
PREVIOUS_VERSION="${PREVIOUS_TAG#v}"

# Writes standard input to a file, with CRLF line endings if the second argument is "crlf"
write_file()
{
	if [ "$2" = "crlf" ]; then
		awk '{ printf "%s\r\n", $0 }' > "$1.tmp"
	else
		cat > "$1.tmp"
	fi

	mv "$1.tmp" "$1"
}

# Prints "crlf" if a file has CRLF line endings, as in a Windows checkout, and "lf" otherwise. The file is
# compared with itself without carriage returns: Git Bash's grep doesn't see them.
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

# Sets a property written on one line, such as <VersionPrefix>0.1.0</VersionPrefix>
set_property()
{
	file="$1"
	name="$2"
	value="$3"

	if ! read_file "$file" | grep -q "<$name>.*</$name>"; then
		echo "$file has no <$name> property to set." >&2
		exit 1
	fi

	endings=$(line_endings "$file")
	read_file "$file" | sed "s|<$name>[^<]*</$name>|<$name>$value</$name>|" | write_file "$file" "$endings"
}

set_property Directory.Build.props VersionPrefix "$VERSION_PREFIX"
set_property Directory.Build.props VersionSuffix "$VERSION_SUFFIX"
set_property src/Directory.Build.props PackageValidationBaselineVersion "$PREVIOUS_VERSION"

# Public API: shipped becomes (shipped - removed) + added, sorted, under the #nullable line both files keep
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

for unshipped in src/*/PublicAPI.Unshipped.txt; do
	project=$(dirname "$unshipped")
	shipped="$project/PublicAPI.Shipped.txt"
	endings=$(line_endings "$unshipped")

	read_file "$shipped" | grep -v -e '^#nullable enable$' -e '^[[:space:]]*$' > "$WORK/shipped" || true
	read_file "$unshipped" | grep -v -e '^#nullable enable$' -e '^[[:space:]]*$' > "$WORK/unshipped" || true
	grep '^\*REMOVED\*' "$WORK/unshipped" | sed 's/^\*REMOVED\*//' > "$WORK/removed" || true
	grep -v '^\*REMOVED\*' "$WORK/unshipped" > "$WORK/added" || true

	# grep -f with an empty pattern file would match nothing and keep nothing, so only filter when needed
	if [ -s "$WORK/removed" ]; then
		grep -v -x -F -f "$WORK/removed" "$WORK/shipped" > "$WORK/kept" || true
	else
		cp "$WORK/shipped" "$WORK/kept"
	fi

	{
		echo '#nullable enable'
		cat "$WORK/kept" "$WORK/added" | LC_ALL=C sort -u
	} | write_file "$shipped" "$endings"

	echo '#nullable enable' | write_file "$unshipped" "$endings"

	echo "$(basename "$project"): $(wc -l < "$WORK/added" | tr -d ' ') shipped, $(wc -l < "$WORK/removed" | tr -d ' ') removed"
done

# README status line
MAJOR="${VERSION_PREFIX%%.*}"
NUGET_LINK="[NuGet](https://www.nuget.org/packages/Latiprimere)"

if [ -n "$VERSION_SUFFIX" ] || [ "$MAJOR" = "0" ]; then
	STATUS="> **Status:** preview. Version $VERSION is on $NUGET_LINK for evaluation. The API may still change before 1.0, so it isn't ready for production use yet."
else
	STATUS="> **Status:** version $VERSION is on $NUGET_LINK."
fi

if ! read_file README.md | grep -q '^> \*\*Status:\*\*'; then
	echo "README.md has no '> **Status:**' line to update." >&2
	exit 1
fi

endings=$(line_endings README.md)
read_file README.md | awk -v status="$STATUS" '/^> \*\*Status:\*\*/ { print status; next } { print }' | write_file README.md "$endings"

echo "Prepared $VERSION. Previous release: ${PREVIOUS_VERSION:-none}."
