#!/bin/sh
# Waits until nuget.org serves every package at a version: each package's flat-container index, which restore reads,
# lists it. nuget.org takes minutes to index a new version after it's pushed. Once post-release.sh has run, main's
# package validation compares with the version just released, and a restore before nuget.org serves it fails on every
# runner. So the Release workflow runs this before post-release.sh and the post-release build.
#
# Usage: sh tools/releasing/wait-for-packages.sh <version> [timeout in seconds, default 1800], from the repository
# root. The packages are the projects in src/. Exits 1 if any package still isn't served when the time is up.
set -eu

if [ $# -lt 1 ] || [ $# -gt 2 ]; then
	echo "Usage: sh tools/releasing/wait-for-packages.sh <version> [timeout in seconds]" >&2
	exit 1
fi

# nuget.org's flat container uses lowercase IDs and versions
VERSION=$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')
TIMEOUT="${2:-1800}"
INTERVAL=30

if ! ls src/*/*.csproj > /dev/null 2>&1; then
	echo "Run this from the repository root: there are no projects in src/." >&2
	exit 1
fi

# Whether nuget.org's index of a package lists the version
is_served()
{
	index=$(curl --fail --silent --max-time 30 "https://api.nuget.org/v3-flatcontainer/$1/index.json") || return 1
	printf '%s' "$index" | tr -d ' \r\n' | sed -n 's/.*"versions":\[\([^]]*\)\].*/\1/p' | tr ',' '\n' | tr -d '"' \
		| grep -qx "$VERSION"
}

start=$(date +%s)

while :; do
	waiting=""

	for project in src/*/*.csproj; do
		package=$(basename "$project" .csproj | tr '[:upper:]' '[:lower:]')

		if ! is_served "$package"; then
			waiting="$waiting $package"
		fi
	done

	if [ -z "$waiting" ]; then
		echo "nuget.org serves every package at $VERSION."
		exit 0
	fi

	elapsed=$(($(date +%s) - start))

	if [ "$elapsed" -ge "$TIMEOUT" ]; then
		echo "After ${elapsed}s, nuget.org still doesn't serve $VERSION of:$waiting" >&2
		exit 1
	fi

	echo "Waiting for nuget.org to serve $VERSION of:$waiting (${elapsed}s so far)"
	sleep "$INTERVAL"
done
