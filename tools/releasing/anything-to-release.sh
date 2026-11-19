#!/bin/sh
# Prints "true" if the commit given (HEAD by default) has anything the latest release doesn't, and "false" if its
# files are exactly the latest release's (docs/releasing.md, ADR 0030).
#
# The latest release is the v<version> tag with the highest build number, as next-version.sh counts them. "false" is
# what merging a post-release pull request's commit, and nothing else, from main into release gives: the release
# commit's files again, which would only republish the same packages under a new version.
#
# Usage: sh tools/releasing/anything-to-release.sh [<commit>], from the repository root, with the tags fetched.
set -eu

COMMIT="${1:-HEAD}"

if ! TREE=$(git rev-parse --verify --quiet "$COMMIT^{tree}"); then
	echo "There's no commit '$COMMIT'." >&2
	exit 1
fi

# The tags of <major>.0.<n> versions, highest build number last
LATEST_TAG=$(git tag --list 'v*' \
	| sed -n 's/^\(v[1-9][0-9]*\.0\.\([0-9][0-9]*\)\(-.*\)\{0,1\}\)$/\2 \1/p' \
	| sort -n \
	| tail -n 1 \
	| cut -d ' ' -f 2)

if [ -z "$LATEST_TAG" ]; then
	echo true
	exit 0
fi

if [ "$(git rev-parse "$LATEST_TAG^{tree}")" = "$TREE" ]; then
	echo "$COMMIT has the same files as $LATEST_TAG, the latest release." >&2
	echo false
else
	echo true
fi
