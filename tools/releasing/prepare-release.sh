#!/bin/sh
# Makes the changes of a release commit (docs/releasing.md):
#
#   - sets the version in Directory.Build.props;
#   - marks the public API as shipped: moves each src/*/PublicAPI.Unshipped.txt into PublicAPI.Shipped.txt, and
#     drops the shipped lines that *REMOVED* entries name;
#   - has packing compare the packages with the previous release, if there was one
#     (PackageValidationBaselineVersion in src/Directory.Build.props). main already does, after the previous release's
#     post-release commit (post-release.sh); this sets it for a first release, or if that commit wasn't made;
#   - updates the version in README.md's status line, first checking the line says "preview" for a preview and
#     "stable" for a stable version, and stopping if not;
#   - turns the "## Unreleased" heading of CHANGELOG.md into "## <version> - <today>".
#
# Usage: sh tools/releasing/prepare-release.sh <version>, for example 11.0.42, from the repository root.
# The release workflow runs it, with the version from next-version.sh; it works locally too (Linux, macOS, or Git
# Bash on Windows). It only changes files: committing them is up to whoever runs it. Each file keeps its line
# endings. It needs to reach nuget.org, to find the previous release.
set -eu

VERSION="${1:-}"

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
PUBLISHED=$(sh "$(dirname "$0")/published-versions.sh")

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

# The previous release's post-release pull request must be merged first, so its release commit, the one tagged, is
# in this one's history. Otherwise its changelog notes would still be under "## Unreleased", and would be dated
# again as this version's, and its public API would be unshipped still. The tag is checked, not the changelog: a
# release without notes has no heading of its own (11.0.5-preview didn't).
if [ -n "$PREVIOUS_VERSION" ]; then
	if ! git rev-parse -q --verify "refs/tags/v$PREVIOUS_VERSION" > /dev/null; then
		echo "$PREVIOUS_VERSION is on nuget.org but not tagged v$PREVIOUS_VERSION here. If its Release run failed after publishing, re-run its failed jobs, which tag it; otherwise fetch the tags." >&2
		exit 1
	fi

	if ! git merge-base --is-ancestor "v$PREVIOUS_VERSION" HEAD; then
		echo "v$PREVIOUS_VERSION isn't in this commit's history. Merge the pull request 'Post-release $PREVIOUS_VERSION' into main, then main into release." >&2
		exit 1
	fi
fi

# Every release needs notes under "## Unreleased", which become its changelog entry. Without them it would have no
# entry, as 11.0.9-preview had none, and users would get no word of what changed, so stop before anything changes. A
# release with nothing to tell users says so in a line. This also catches notes written under a released version's
# heading, as 11.0.5's were under 11.0.3's, which would credit them to the wrong release. (Corrections to earlier
# entries can go out with the next release's notes.)
if ! tr -d '\r' < CHANGELOG.md \
	| awk '$0 == "## Unreleased" { found = 1; next } found && /^## / { exit } found && NF && !/^#/ { notes = 1 } END { exit !notes }'; then
	echo "CHANGELOG.md has no notes under '## Unreleased'. Write this release's notes there, above the newest release's heading, on main, then merge main into release again. For a release with nothing to tell users, say so in a line." >&2
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

# The README's status must match the release, checked before anything changes: a stable release needs the wording
# switch-to-stable.sh writes, and a preview mustn't be published while the README says stable, as when
# RELEASE_PREVIEW was left true after the switch (docs/releasing.md, "The first stable release")
sh "$(dirname "$0")/check-readme-status.sh" "$VERSION"

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

# Changelog: the notes written under "## Unreleased", checked above, become this version's
endings=$(line_endings CHANGELOG.md)
read_file CHANGELOG.md \
	| awk -v heading="## $VERSION - $(date -u +%Y-%m-%d)" '!done && $0 == "## Unreleased" { print heading; done = 1; next } { print }' \
	| write_file CHANGELOG.md "$endings"

echo "Prepared $VERSION. Previous release: ${PREVIOUS_VERSION:-none}."
