#!/usr/bin/env bash
# Sets up the virtual printers and runs Latiprimere's end-to-end scenarios on Linux or macOS. Nothing prints on paper.
#
#   1. Uses a Mac's own test queues (tools/macos-test-printers) if they are installed; otherwise starts the CUPS test
#      container (tools/virtual-printers) if Docker is running, unless --no-container.
#   2. Sets the LATIPRIMERE_TEST_* variables for this run.
#   3. Runs the console harness (tests/Latiprimere.TestHarness), or with --xunit the xUnit host for both target
#      frameworks, and leaves a report in out/harness.
#
# Usage: tools/test-harness/run.sh [--xunit] [--framework net11.0] [--no-container] [--down]
#                                  [--packages] [--aot] [--host-network] [filter ...]
#   filter:         scenario IDs, ID prefixes ending in * (quote them: 'cups.*') or areas (such as Lifecycle); none
#                   runs all.
#   --packages:     packs Latiprimere into out/harness-packages/<time> and runs the scenarios against those packages.
#   --aot:          publishes the harness with Native AOT into out/harness-aot and runs the native program; fails on
#                   any trimming or AOT warning.
#   --host-network: Linux only: runs the container on the host's network, so the mDNS discovery scenario can find its
#                   ippeveprinter. CUPS then takes the host's port 631.
# See docs/test-harness-plan.md.

set -euo pipefail

repository="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
compose_files=(-f "$repository/tools/virtual-printers/compose.yaml")
harness_project="$repository/tests/Latiprimere.TestHarness"
framework="net11.0"
use_xunit=false
use_container=true
stop_container=false
use_packages=false
use_aot=false
filters=()

while [[ $# -gt 0 ]]; do
	case "$1" in
		--xunit) use_xunit=true ;;
		--framework) framework="$2"; shift ;;
		--no-container) use_container=false ;;
		--down) stop_container=true ;;
		--packages) use_packages=true ;;
		--aot) use_aot=true ;;
		--host-network) compose_files+=(-f "$repository/tools/virtual-printers/compose.host-network.yaml"); export LATIPRIMERE_TEST_MDNS_NAME="Virtual IPP Printer" ;;
		-h|--help) sed -n '2,19p' "${BASH_SOURCE[0]}"; exit 0 ;;
		*) filters+=("$1") ;;
	esac
	shift
done

if [[ "$use_xunit" == true && ( "$use_packages" == true || "$use_aot" == true ) ]]; then
	echo "--packages and --aot run the console harness; leave out --xunit." >&2
	exit 2
fi

export LATIPRIMERE_TEST_E2E=1
unset LATIPRIMERE_TEST_CUPS_SERVER LATIPRIMERE_TEST_CUPS_OUTPUT LATIPRIMERE_TEST_CUPS_SETUP LATIPRIMERE_TEST_IPP_PRINTER
started_container=false

if [[ "$(uname)" == "Darwin" ]] && lpstat -p Raw > /dev/null 2>&1 && lpstat -p Virtual-IPP > /dev/null 2>&1; then
	# A Mac with tools/macos-test-printers installed: its own CUPS, without CUPS-PDF and Secure
	echo "== Using this Mac's test queues"
	export LATIPRIMERE_TEST_CUPS_SERVER=127.0.0.1:631
	export LATIPRIMERE_TEST_CUPS_SETUP=macos
	export LATIPRIMERE_TEST_CUPS_OUTPUT=/Users/Shared/LatiprimereTestOutput
	export LATIPRIMERE_TEST_IPP_PRINTER=ipp://127.0.0.1:8631/ipp/print
elif [[ "$use_container" == true ]] && command -v docker > /dev/null && docker info > /dev/null 2>&1; then
	# The CUPS test container: addresses are 127.0.0.1, not localhost, because it publishes on IPv4 only
	echo "== Starting the CUPS test container"
	export LATIPRIMERE_TEST_CUPS_OUTPUT="$repository/out/cups-output"
	mkdir -p "$LATIPRIMERE_TEST_CUPS_OUTPUT"

	# After the host restarts, the container is left stopped, and starting it again reuses everything the last run
	# left in it. So a container that is stopped, or running but unhealthy, is made afresh from the image.
	recreate=()
	container_state="$(docker inspect latiprimere-cups --format '{{.State.Status}}{{if .State.Health}} {{.State.Health.Status}}{{end}}' 2> /dev/null || true)"

	if [[ -n "$container_state" && "$container_state" != "running healthy" && "$container_state" != "running starting" ]]; then
		echo "== The test container is $container_state, as after a restart: making it afresh"
		recreate=(--force-recreate)
	fi

	docker compose "${compose_files[@]}" up -d --build --wait ${recreate[@]+"${recreate[@]}"}
	started_container=true
	export LATIPRIMERE_TEST_CUPS_SERVER=127.0.0.1:631
	export LATIPRIMERE_TEST_IPP_PRINTER=ipp://127.0.0.1:8631/ipp/print
elif [[ "$use_container" == true ]]; then
	echo "Docker is not running and no Mac test queues are installed, so the CUPS and direct IPP scenarios will skip." >&2
fi

stop() {
	if [[ "$stop_container" == true && "$started_container" == true ]]; then
		echo "== Stopping the CUPS test container"
		docker compose "${compose_files[@]}" down
	fi
}
trap stop EXIT

build_properties=()

if [[ "$use_packages" == true ]]; then
	# A version of their own, so NuGet's cache can never hand back an older build of the same version
	stamp="$(date +%Y%m%d%H%M%S)"
	package_folder="$repository/out/harness-packages/$stamp"
	prefix="$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$repository/Directory.Build.props" | head -n 1)"
	suffix="preview.e2e.$stamp"
	version="$prefix-$suffix"

	echo "== Packing Latiprimere $version into $package_folder"
	while IFS= read -r project; do
		dotnet pack "$project" --configuration Release --output "$package_folder" "-p:VersionSuffix=$suffix" -nologo
	done < <(find "$repository/src" -name '*.csproj')

	# Latiprimere's packages from that folder, everything else from nuget.org as the repository's nuget.config has it
	cat > "$package_folder/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
	<packageSources>
		<clear />
		<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
		<add key="latiprimere-packed" value="$package_folder" />
	</packageSources>
	<packageSourceMapping>
		<clear />
		<packageSource key="nuget.org">
			<package pattern="*" />
		</packageSource>
		<packageSource key="latiprimere-packed">
			<package pattern="Latiprimere" />
			<package pattern="Latiprimere.*" />
		</packageSource>
	</packageSourceMapping>
</configuration>
EOF
	build_properties=("-p:LatiprimereFromPackages=$package_folder" "-p:LatiprimerePackageVersion=$version")
fi

status=0

if [[ "$use_xunit" == true ]]; then
	echo "== Running the scenarios with xUnit"
	dotnet test --project "$repository/tests/Latiprimere.EndToEnd.Tests" || status=$?
elif [[ "$use_aot" == true ]]; then
	# The samples run as programs, so they are built as usual first
	echo "== Building the samples for $framework"
	for sample in Latiprimere.Cli Latiprimere.Examples; do
		dotnet build "$repository/samples/$sample" --configuration Release --framework "$framework" -nologo
	done

	aot_folder="$repository/out/harness-aot/$framework"
	publish_log="$(mktemp)"
	echo "== Publishing the harness with Native AOT into $aot_folder"
	dotnet publish "$harness_project" --configuration Release --framework "$framework" --use-current-runtime \
		-p:PublishAot=true -p:TrimmerSingleWarn=false --output "$aot_folder" -nologo ${build_properties[@]+"${build_properties[@]}"} | tee "$publish_log"

	# IL2xxx are trimming warnings, IL3xxx AOT ones; either means some code may not work once published
	if grep -E "warning IL[23][0-9]{3}" "$publish_log"; then
		echo "Publishing the harness reported trimming or AOT warnings." >&2
		exit 1
	fi

	echo "== Running the scenarios as a native program"
	"$aot_folder/Latiprimere.TestHarness" run ${filters[@]+"${filters[@]}"} || status=$?
elif [[ "$use_packages" == true ]]; then
	echo "== Building the harness against the packages"
	dotnet build "$harness_project" --configuration Release --framework "$framework" -nologo "${build_properties[@]}"

	echo "== Running the scenarios on $framework, from the packages"
	"$repository/out/packaged/bin/Latiprimere.TestHarness/Release/$framework/Latiprimere.TestHarness" run ${filters[@]+"${filters[@]}"} || status=$?
else
	echo "== Running the scenarios on $framework"
	# The odd expansion keeps an empty array working under set -u in bash 3.2, which macOS has
	dotnet run --project "$harness_project" -f "$framework" -- run ${filters[@]+"${filters[@]}"} || status=$?
fi

exit "$status"
