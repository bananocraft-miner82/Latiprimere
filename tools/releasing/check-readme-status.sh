#!/bin/sh
# Checks README.md's status line agrees with a release's version: "preview" for a preview, such as 11.0.42-preview,
# and "stable" for a stable version, such as 11.0.43. prepare-release.sh runs it before changing anything, so a
# preview isn't published while the README says stable, as when RELEASE_PREVIEW was left true after
# switch-to-stable.sh, and a stable release isn't published with the preview wording (docs/releasing.md, "The first
# stable release").
#
# Usage: sh tools/releasing/check-readme-status.sh <version>, from the repository root. Exits 1, saying what to do, if
# they don't agree.
set -eu

VERSION="${1:-}"

if [ -z "$VERSION" ]; then
	echo "Give the version to check, such as 11.0.42." >&2
	exit 1
fi

if [ ! -f README.md ]; then
	echo "Run this from the repository root: README.md isn't here." >&2
	exit 1
fi

# The word after "> **Status:**", without carriage returns, which Git Bash's sed would keep
STATUS=$(tr -d '\r' < README.md | sed -n -E 's/^> \*\*Status:\*\* ([A-Za-z]+)\..*/\1/p' | head -n 1)

case "$VERSION" in
	*-*) WANTED=preview ;;
	*) WANTED=stable ;;
esac

if [ "$STATUS" = "$WANTED" ]; then
	exit 0
fi

if [ "$WANTED" = stable ]; then
	echo "$VERSION is a stable version, but README.md's status says '$STATUS'. Run tools/releasing/switch-to-stable.sh on main first, or set RELEASE_PREVIEW to true." >&2
else
	echo "$VERSION is a preview, but README.md's status says '$STATUS'. Set the repository variable RELEASE_PREVIEW to false for a stable release." >&2
fi

exit 1
