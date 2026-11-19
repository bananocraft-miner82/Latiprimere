# 0028: Automated releases from the release branch

- Status: Accepted; its build number superseded by [0030](0030-release-numbering.md)
- Date: 2026-11-16

## Context

[ADR 0022](0022-release-process.md) made a release in four manual steps: choose a version, run "Prepare release", merge its pull request, then tag the merge commit by hand. The tag had to match the version in Directory.Build.props. For 0.1.1-preview it didn't: the tag went on the commit before the release commit, release.yml refused it, and nothing was published.

Owner: "simplify the versioning and release process … Ideally, this should be automated, including the version numbering. The version numbering should be \<dotnetmajorversion\>.0.\<build number\>. Once the changes are successfully merged to a release branch, it would be good to have the build and deploy trigger."

## Decision

This replaces ADR 0022's version scheme, its "Prepare release" workflow and its tag trigger. Its package contents, trusted publishing and `release` environment stay.

- **Versions are `<.NET major>.0.<build number>`,** such as `11.0.42`.
  - **Previews end in `-preview`,** such as `11.0.42-preview`, while the repository variable `RELEASE_PREVIEW` is `true` or not set. Owner: "add a preview mode for the time being until the manual testing with real printers can be confirmed". Setting it to `false` makes later releases stable, with no change to the code. Unset counts as `true`, so a missing variable can't release a stable version by mistake.
  - The .NET major is the newest target in `<TargetFrameworks>`, so moving to a new .NET moves the version with it.
  - The build number is the Release workflow's run number: it only grows, needs no state, and a re-run keeps it.
  - [tools/releasing/next-version.sh](../../tools/releasing/next-version.sh) works it out.
  - **One package per release, for every supported .NET.** Each package keeps a build for every target framework, so `11.0.42` serves .NET 10 apps too. Separate packages per .NET, such as `10.0.42` with only `net10.0`, were considered and rejected (owner: "Keep one package"): updating a .NET 10 project would pick `11.0.42` and fail to restore, so users would have to pin `10.0.*`. Supporting another .NET means adding it to `<TargetFrameworks>`.
- **A push to the `release` branch publishes.** Merging a pull request from `main` into `release` is the deliberate act, guarded by branch protection that requires the build. [release.yml](../../.github/workflows/release.yml) then runs four jobs:
  - **prepare** runs [prepare-release.sh](../../tools/releasing/prepare-release.sh) on the merged commit and pushes the result as `post-release/v<version>`;
  - **build** calls [build.yml](../../.github/workflows/build.yml) on that commit: all three runners, integration tests and Native AOT included;
  - **publish** pushes the packages build.yml made, in the `release` environment;
  - **finish** tags the commit and opens a pull request that brings it into `main`.
- **The release commit is built and tagged, not the merge commit.** So the tag has the version in Directory.Build.props and dated changelog notes, and the published packages come from exactly the tagged commit. The tag is made only after publishing succeeds, so every tag is a published version.
- **The script now also dates the changelog:** `## Unreleased` becomes `## <version> - <date>`. The notes are written on `main` beforehand, since the version isn't known until the workflow runs.
- **The package validation baseline is the newest version on nuget.org,** not the newest tag. Package validation downloads the baseline from nuget.org, and a tagged version that never published (as 0.1.1-preview) couldn't be one.
- **The README status line keeps its wording;** only its version changes.
- **`release` is never committed to.** Only merges from `main` reach it; the release commits go to `main` through the post-release pull request. So the two branches never conflict.

## Consequences

- **A release is two merges:** `main` into `release`, then the post-release pull request into `main`. The next release refuses to start until the second is merged, so changelog notes are never dated twice.
- **Versions have gaps** where runs failed, and say nothing about compatibility. Package validation still fails a release with a breaking change, unless it is suppressed deliberately.
- **The first release under this scheme is 11.0.\<n\>-preview,** which sorts after 0.1.0-preview. Once `RELEASE_PREVIEW` is `false`, the README status line's wording ("preview", "before 1.0") needs revising by hand; the script changes only its version.
- **Renaming `release.yml` would restart the build numbers,** and break the trusted publishing policy, which names it.
- **One-time setup changes:** create and protect the `release` branch, after deleting `release/v0.1.1-preview`, whose name conflicts with it; and allow the `release` environment on that branch instead of `v*` tags.
