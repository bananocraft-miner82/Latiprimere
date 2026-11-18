#!/bin/sh
# Prints every version of the Latiprimere package on nuget.org, one per line, oldest first, unlisted ones included
# (docs/releasing.md). next-version.sh, prepare-release.sh and anything-to-release.sh read them from here.
#
# Usage: sh tools/releasing/published-versions.sh. It needs to reach nuget.org, and fails if it can't, rather than
# print nothing: an empty list would make the next release look like the first.
set -eu

NUGET_INDEX=https://api.nuget.org/v3-flatcontainer/latiprimere/index.json

if ! INDEX=$(curl --fail --silent --show-error --max-time 30 --retry 3 "$NUGET_INDEX"); then
	echo "Couldn't read the published versions from $NUGET_INDEX." >&2
	exit 1
fi

# The last version gets a line ending too, so the output can be joined to other lists
{
	printf '%s' "$INDEX" | tr -d ' \r\n' | sed -n 's/.*"versions":\[\([^]]*\)\].*/\1/p' | tr ',' '\n' | tr -d '"'
	echo
} | sed '/^$/d'
