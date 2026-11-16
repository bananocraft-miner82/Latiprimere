#!/bin/sh
# Makes the changes of a release commit (docs/releasing.md):
#
#   - sets the version in Directory.Build.props;
#   - marks the public API as shipped: moves each src/*/PublicAPI.Unshipped.txt into PublicAPI.Shipped.txt, and
#     drops the shipped lines that *REMOVED* entries name;
#   - has packing compare the packages with the previous release, if there was one
#     (PackageValidationBaselineVersion in src/Directory.Build.props);
#   - updates the version in README.md's status line;
#   - turns the "## Unreleased" heading of CHANGELOG.md into "## <version> - <today>".
#
# Usage: sh tools/releasing/prepare-release.sh <version>, for example 11.0.42, from the repository root.
# The release workflow runs it, with the version from next-version.sh; it works locally too (Linux, macOS, or Git
# Bash on Windows). It only changes files: committing them is up to whoever runs it. Each file keeps its line
# endings. It needs to reach nuget.org, to find the previous release.
set -eu

VERSION="${1:-}"
NUGET_INDEX=https://api.nuget.org/v3-flatcontainer/latiprimere/index.json

if ! printf '%s\n' "$VERSION" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$'; then
	echo "Give the version to release, such as 11.0.42: '$VERSION' isn't one." >&2
	exit 1
fi

if [ ! -f Directory.Build.props ] || [ ! -f src/Directory.Build.props ] || [ ! -f README.md ] || [ ! -f CHANGELOG.md ]; then
	echo "Run this from the repository root." >&2
	exit 1
fi

if git rev-parse -q --verify "refs/tags/v$VERSION" > /dev/null; then
	echo "v$VERSION is already tagged, and a published version can't be published again." >&2
	exit 1
fi

# Every version of the Latiprimere package on nuget.org, oldest first, unlisted ones included
if ! INDEX=$(curl --fail --silent --show-error --max-time 30 --retry 3 "$NUGET_INDEX"); then
	echo "Couldn't read the published versions from $NUGET_INDEX." >&2
	exit 1
fi

PUBLISHED=$(printf '%s' "$INDEX" | tr -d ' \r\n' | sed -n 's/.*"versions":\[\([^]]*\)\].*/\1/p' | tr ',' '\n' | tr -d '"')

if printf '%s\n' "$PUBLISHED" | grep -qxF "$VERSION"; then
	echo "$VERSION is already on nuget.org, and a published version can't be published again." >&2
	exit 1
fi

VERSION_PREFIX="${VERSION%%-*}"
VERSION_SUFFIX=""

case "$VERSION" in
	*-*) VERSION_SUFFIX="${VERSION#*-}" ;;
esac

# The previous release, if any: the newest version on nuget.org. Package validation downloads it from there, so a
# version that was tagged but never published can't be the baseline.
PREVIOUS_VERSION=$(printf '%s\n' "$PUBLISHED" | sed '/^$/d' | tail -n 1)

# The previous release's post-release pull request must be merged first. Otherwise its notes would still be under
# "## Unreleased", and would be dated again as this version's.
if [ -n "$PREVIOUS_VERSION" ] && ! tr -d '\r' < CHANGELOG.md | grep -q "^## $PREVIOUS_VERSION - "; then
	echo "CHANGELOG.md has no '## $PREVIOUS_VERSION - <date>' heading. Merge the pull request 'Post-release $PREVIOUS_VERSION' into main, then main into release." >&2
	exit 1
fi

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

# README status line: only the version in it changes, as in "Version 11.0.42 is on [NuGet]", so its wording is kept
if ! read_file README.md | grep -Eq '^> \*\*Status:\*\*.*[Vv]ersion [^ ]+ is on'; then
	echo "README.md has no '> **Status:**' line saying 'Version <version> is on' to update." >&2
	exit 1
fi

endings=$(line_endings README.md)
read_file README.md \
	| sed -E "/^> \*\*Status:\*\*/ s/([Vv]ersion) [^ ]+ is on/\1 $VERSION is on/" \
	| write_file README.md "$endings"

# Changelog: the notes written under "## Unreleased" become this version's. Without them the release still goes
# ahead, with a warning, and the website shows no entry for it.
if read_file CHANGELOG.md | grep -qx '## Unreleased'; then
	endings=$(line_endings CHANGELOG.md)
	read_file CHANGELOG.md \
		| awk -v heading="## $VERSION - $(date -u +%Y-%m-%d)" '!done && $0 == "## Unreleased" { print heading; done = 1; next } { print }' \
		| write_file CHANGELOG.md "$endings"
else
	echo "::warning::CHANGELOG.md has no '## Unreleased' notes, so $VERSION has no entry in it."
fi

echo "Prepared $VERSION. Previous release: ${PREVIOUS_VERSION:-none}."
