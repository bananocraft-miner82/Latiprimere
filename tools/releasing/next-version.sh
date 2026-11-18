#!/bin/sh
# Prints the version of the next release: <.NET major>.0.<build number>, such as 11.0.42, or 11.0.42-preview for a
# preview (docs/releasing.md, ADR 0030).
#
#   - The .NET major is the newest .NET the packages target, from <TargetFrameworks> in Directory.Build.props: with
#     net10.0;net11.0 it is 11. Adding net12.0 makes the next release 12.0.<build number>.
#   - The build number is one more than the highest of any release so far, on nuget.org or tagged v<version> here,
#     whatever its .NET major: after 11.0.5-preview comes 11.0.6-preview. So a run that failed or was cancelled
#     before publishing leaves no gap. Tags count too because nuget.org can take a few minutes to list a new
#     version, and a release that published but failed before tagging is still on nuget.org. Versions from before
#     this scheme, such as 0.1.0-preview, don't count. The first release under it would be <major>.0.1.
#   - With --preview, the version ends in -preview, such as 11.0.42-preview: nuget.org lists it as a prerelease,
#     which projects only get when they ask for prereleases.
#
# Usage: sh tools/releasing/next-version.sh [--preview], from the repository root, with the tags fetched. It needs to
# reach nuget.org.
set -eu

SUFFIX=""

case "${1:-}" in
	"") ;;
	--preview) SUFFIX="-preview" ;;
	*)
		echo "The only option is --preview: '$1' isn't one." >&2
		exit 1
		;;
esac

if [ ! -f Directory.Build.props ]; then
	echo "Run this from the repository root." >&2
	exit 1
fi

MAJOR=$(tr -d '\r' < Directory.Build.props \
	| sed -n 's:.*<TargetFrameworks>\(.*\)</TargetFrameworks>.*:\1:p' \
	| tr ';' '\n' \
	| sed -n 's/^[[:space:]]*net\([0-9][0-9]*\)\.[0-9][0-9]*[[:space:]]*$/\1/p' \
	| sort -n \
	| tail -n 1)

if [ -z "$MAJOR" ]; then
	echo "Directory.Build.props has no <TargetFrameworks> with a target such as net11.0." >&2
	exit 1
fi

PUBLISHED=$(sh "$(dirname "$0")/published-versions.sh")
TAGGED=$(git tag --list 'v*' | sed 's/^v//')

# The build numbers of <major>.0.<n> versions, majors from 1 up, with or without a suffix
LAST_BUILD=$(printf '%s\n%s\n' "$PUBLISHED" "$TAGGED" \
	| sed -n 's/^[1-9][0-9]*\.0\.\([0-9][0-9]*\)\(-.*\)\{0,1\}$/\1/p' \
	| sort -n \
	| tail -n 1)

echo "$MAJOR.0.$(( ${LAST_BUILD:-0} + 1 ))$SUFFIX"
