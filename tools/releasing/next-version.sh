#!/bin/sh
# Prints the version of a release: <.NET major>.0.<build number>, such as 11.0.42, or 11.0.42-preview for a preview
# (docs/releasing.md).
#
#   - The .NET major is the newest .NET the packages target, from <TargetFrameworks> in Directory.Build.props: with
#     net10.0;net11.0 it is 11. Adding net12.0 makes the next release 12.0.<build number>.
#   - The build number is given, and must only grow. The release workflow passes its run number.
#   - With --preview, the version ends in -preview, such as 11.0.42-preview: nuget.org lists it as a prerelease,
#     which projects only get when they ask for prereleases.
#
# Usage: sh tools/releasing/next-version.sh <build number> [--preview], from the repository root.
set -eu

BUILD_NUMBER="${1:-}"
SUFFIX=""

case "${2:-}" in
	"") ;;
	--preview) SUFFIX="-preview" ;;
	*)
		echo "The only option is --preview: '$2' isn't one." >&2
		exit 1
		;;
esac

if ! printf '%s\n' "$BUILD_NUMBER" | grep -Eq '^[0-9]+$'; then
	echo "Give the build number, a whole number such as 42: '$BUILD_NUMBER' isn't one." >&2
	exit 1
fi

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

echo "$MAJOR.0.$BUILD_NUMBER$SUFFIX"
