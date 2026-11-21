#!/bin/sh
# Writes a CycloneDX software bill of materials (SBOM) for each package, as the EU Cyber Resilience Act asks of a
# product's manufacturer (ADR 0051). Each one lists the package's dependencies, direct and transitive, with their
# versions, licences and hashes. Build-only packages, such as analyzers, are listed with scope "excluded", so
# they're on record without being counted as part of what ships.
#
# dotnet pack puts each SBOM in its package, at sbom/<package>.cdx.json, when SbomDirectory is set to the same
# directory (src/Directory.Build.props), so every version on nuget.org carries its own. The manufacturer's details
# come from sbom-metadata.cdx.xml.
#
# Usage: sh tools/releasing/generate-sboms.sh <output directory>, from the repository root, after a restore. It needs
# the CycloneDX tool from dotnet-tools.json (dotnet tool restore).
set -eu

if [ $# -ne 1 ]; then
	echo "Usage: sh tools/releasing/generate-sboms.sh <output directory>" >&2
	exit 1
fi

OUTPUT="$1"
METADATA="tools/releasing/sbom-metadata.cdx.xml"

if [ ! -f "$METADATA" ]; then
	echo "Run this from the repository root: $METADATA isn't here." >&2
	exit 1
fi

mkdir -p "$OUTPUT"

# One version for every package (Directory.Build.props)
VERSION=$(dotnet msbuild src/Latiprimere.Abstractions/Latiprimere.Abstractions.csproj -getProperty:PackageVersion)
count=0

for project in src/*/*.csproj; do
	# The package ID is the project's name
	package=$(basename "$project" .csproj)

	# -rs and -ipr list the packages a project reference becomes, such as Latiprimere.Abstractions, by their NuGet
	# names. -dpr: the restore has already been done
	dotnet dotnet-CycloneDX "$project" \
		--output "$OUTPUT" \
		--filename "$package.cdx.json" \
		--output-format Json \
		--recursive \
		--include-project-references \
		--set-nuget-purl \
		--set-type Library \
		--set-name "$package" \
		--set-version "$VERSION" \
		--import-metadata-path "$METADATA" \
		--disable-package-restore \
		> /dev/null

	if [ ! -s "$OUTPUT/$package.cdx.json" ]; then
		echo "No SBOM was written for $package." >&2
		exit 1
	fi

	count=$((count + 1))
done

echo "Wrote $count SBOMs for version $VERSION to $OUTPUT."
