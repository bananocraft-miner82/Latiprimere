# 0022: Release process and package contents

- Status: Proposed
- Date: 2026-11-13

## Context

The first preview, 0.1.0-preview, is the last item of Phase 1, and publishing early claims the `Latiprimere` name on nuget.org (plan.md, risks). Six projects are packable. Until now nothing set a version, a package README or symbols, and nothing published.

## Decision

The choices are mine, for the owner to review. The steps are in [releasing.md](../releasing.md).

- **One version for all packages,** set in [Directory.Build.props](../../Directory.Build.props) (`VersionPrefix` and `VersionSuffix`).
  - The packages depend on each other at the same version, and releasing them together keeps that simple.
  - The first is `0.1.0-preview`, as in plan.md. Later previews are `0.1.0-preview.2` and so on, which sort after it.
- **Publishing is triggered by a tag:** pushing `v<version>` runs [release.yml](../../.github/workflows/release.yml). It builds, tests, packs and pushes.
  - It refuses a tag that doesn't match the version in Directory.Build.props.
  - It runs in a GitHub environment, `release`, which can require the owner's approval.
- **nuget.org trusted publishing, not an API key.** `NuGet/login` exchanges GitHub's identity token for a key that lasts one hour, under a policy on nuget.org that names the repository, `release.yml` and the `release` environment. So there is no long-lived secret to leak or rotate; the only secret is the nuget.org user name.
- **Package contents:**
  - **One README for all six packages** (`src/PACKAGE-README.md`), with the package list, a quick start and the licence summary. Its links are absolute.
  - **The licence file,** as ADR 0015 decided.
  - **Symbols** in `.snupkg` files.
  - **Source Link** to the commit. CI builds are deterministic (`ContinuousIntegrationBuild`).
  - **Tags** per package.
- **The release commit is made by a workflow:** "Prepare release" ([prepare-release.yml](../../.github/workflows/prepare-release.yml)), started by hand with a version, runs [tools/releasing/prepare-release.sh](../../tools/releasing/prepare-release.sh). The script:
  - sets the version;
  - marks the public API as shipped;
  - sets the package validation baseline to the previous release;
  - updates the README status line.

  The workflow then builds and packs, opens a pull request, and starts the build on all three runners. Merging, and tagging the merge commit, stay manual: tagging is the deliberate act that publishes, and the progress notes need writing.
  - **The pull request's build is started explicitly** (`gh workflow run build.yml`), because a push made with the workflow's own token starts no workflows.
  - **The script works locally too,** and keeps each file's line endings, so it can be tried on a Windows checkout.
- **Package validation** runs on every pack. From the second release, the baseline makes it compare against the previous version.
- **Every change is packed in CI:** the Ubuntu runner packs, and keeps the packages for 7 days. So packaging problems show up before a release.
- **The public API is marked shipped as part of each release,** by moving `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt` (ADR 0018). It isn't done before then, so changes made before the release stay visible as unshipped. The shipped file is kept sorted (ordinal), which keeps its diffs small.

## Consequences

- **One-time setup by the owner:**
  - the nuget.org account;
  - the trusted publishing policy;
  - the `release` environment;
  - the `NUGET_USER` secret;
  - the repository setting that lets workflows open pull requests;
  - optionally, the reservation of the `Latiprimere` ID prefix.
- **Every package gets a new version at each release,** even if it didn't change. That's simpler for users, who can keep all Latiprimere packages at one version.
- **A version can't be taken back,** only unlisted. The tag check and the approval step guard against publishing by mistake.
- **The package icon** came after 0.1.0-preview, which shows nuget.org's default. Later releases carry `src/icon.png`.
