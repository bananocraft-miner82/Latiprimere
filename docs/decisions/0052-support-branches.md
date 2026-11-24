# 0052: Support branches for older .NET lines

- Status: Proposed
- Date: 2026-11-23

## Context

[SECURITY.md](../../SECURITY.md) promises each stable .NET line, such as `11.0.x`, security updates for at least five years from its first stable release. The fix goes into a new version of that line ([ADR 0051](0051-security-policy-sbom-and-cra.md)). Only the latest preview is fixed, so the promise starts with 1.0.

When .NET 12 brings a `12.0.x` line, `main` and `release` move on to it ([ADR 0030](0030-release-numbering.md): the major is the newest .NET the packages target). From then on, a security fix for `11.0.x` needs a branch that still has 11.0's code, and a way to release from it. The Release workflow can't do that yet. It runs only on pushes to `release`, and its scripts assume one line:

- **[prepare-release.sh](../../tools/releasing/prepare-release.sh)** takes the previous release to be the newest version on nuget.org. On an 11.0 branch, that would be a 12.0 version. The script would stop because that version's tag isn't in the branch's history. Without that check, it would compare the packages with the wrong release.
- **[anything-to-release.sh](../../tools/releasing/anything-to-release.sh)** compares the merged commit with the tag that has the highest build number, whatever its line.
- **[release.yml](../../.github/workflows/release.yml)** opens the post-release pull request into `main`, and reads `RELEASE_PREVIEW`, a repository-wide setting, to choose between a preview and a stable version.
- **The `release` environment** allows deployments from the `release` branch only ([releasing.md](../releasing.md), one-time setup).

Some things already work:

- **[next-version.sh](../../tools/releasing/next-version.sh) numbers builds across every line.** A fix after `12.0.40` would be `11.0.41`: never taken, and higher than every earlier `11.0` version, so NuGet offers it as the update within the line.
- **nuget.org's trusted publishing policy** names the workflow file and the environment, not a branch.
- **build.yml builds every pull request,** whatever its base branch.
- **The website names the newest tag's version.** An `11.0` fix released after `12.0` won't replace `12.0` on the home page.

Owner, 2026-11-23: "Would it also be beneficial for supporting older releases in the future to branch off each major version with its own release branch […] that can be updated with security updates once a newer version is released? Is this something we can do with the package release process automatically?"

## Decision

**A support branch per stable line that has been superseded,** named `support/<major>.0`, such as `support/11.0`.

- **Not `release/11.0`:** Git can't have a branch `release` and branches `release/…` at the same time ([releasing.md](../releasing.md), one-time setup).
- **Not `v11.0`:** the release scripts and the website treat `v*` names as release tags. A branch named like one reads as a tag, and a tag of the same name would make it ambiguous.

**The Release workflow creates it.** When a release is the first of a new major, the `finish` job creates `support/<previous major>.0` from the previous line's newest tag, and says so in the run's summary. It does this only if that line had a stable release. A line that was only ever previews isn't supported, as SECURITY.md says.

**Merging into a support branch releases from it,** through the same workflow:

- **Trigger:** release.yml runs on pushes to `release` and to `support/*`.
- **One release at a time, across every line:** the `release` concurrency group stays shared. next-version.sh picks one more than the highest build number of any line, so two lines releasing at once could pick the same number.
- **The version:** next-version.sh takes the major from the branch name (`support/11.0` gives 11) and stops if the branch's target frameworks have a newer .NET. Support releases are always stable, whatever `RELEASE_PREVIEW` says.
- **The previous release** is the newest version of the same line, in prepare-release.sh and in anything-to-release.sh. prepare-release.sh's other checks apply as they are: the previous release's tag must be in the branch's history, and the release needs notes under `## Unreleased`.
- **Fixes only:** prepare-release.sh refuses a support release whose `PublicAPI.Unshipped.txt` files list anything. A fix that can't be made without new API needs an ADR of its own first.
- **Package validation** compares with the line's previous release, as `main` does with its own.
- **The post-release pull request** goes into the support branch it came from, with its baseline set to the release just published (post-release.sh, unchanged).

**Fixes reach a support branch by backport.**

- **A fix is made on `main` first,** unless the code it fixes is no longer there.
- **The "Backport" workflow** (new) handles labelled pull requests. When one labelled `backport 11.0` is merged into `main`, it cherry-picks the pull request's changes with `git cherry-pick -x` onto a new branch from `support/11.0`, and opens a pull request into it.
  - The fix's changelog note comes with it. Before merging, check that the note is under `## Unreleased`.
  - If the cherry-pick conflicts, the workflow comments on the original pull request instead, and the backport is done by hand.
- **Merging the backport releases it.** A support branch has no `main` of its own in front of it, so each merge into it is a release, as each merge into `release` is. Several fixes can go out together by merging them into one backport branch first. Merging a support release's post-release pull request publishes nothing, as anything-to-release.sh finds the release's files again.

**Every line's releases are in `main`'s changelog.**

- **A pull request into `main`:** after a support release, the `finish` job opens one that adds the release's dated entry to `CHANGELOG.md` under `## Unreleased`. Its branch starts with `post-release/`, so merging it rebuilds the website, as the "Update website" workflow does for post-release pull requests.
- **The website shows the entry at once:** `scripts/sync-docs.ts` in the website's repository adds the releases that the newest tag of each line has and `main` doesn't. Today it does this for the newest tag only.

**A line's support ends five years after its first stable release.** Its end date goes in SECURITY.md's table once the first stable release sets it. The support branch then gets no more releases, and stays, read-only, for its history.

## Consequences

- **Nothing is needed until a stable line is superseded.** That is the first `12.0` release after `11.0` goes stable, expected with .NET 12 in November 2027, before the Cyber Resilience Act's other duties apply on 11 December 2027. The script and workflow changes should be in place and tested before then.
- **The owner's one-time setup gains:**
  - **`support/*` in the `release` environment's deployment branches;**
  - **a ruleset for `support/*`,** as for `release`: a pull request, the three "Build and test" checks, and merge commits only. A ruleset that matches a pattern covers each support branch as the workflow creates it.
  - **the `backport <major>.0` labels,** one for each supported line.
- **CI tests the line-aware scripts** in test-release-scripts.sh, with versions from two lines, as for the other release scripts, which run too rarely to find their mistakes in use.
- **Build numbers aren't in date order within a line.** After `12.0.40` and `11.0.41`, the next `12.0` release is `12.0.42`. The numbers stay unique and rise within each line, which is what NuGet and package validation need.
- **The website shows the newest line's docs only.** Readers on `11.0.x` read `12.0` docs, which mark what is new in `12.0`. Docs per line would be a website decision of its own.
- **Each security advisory names the fixed version of each affected line,** such as `11.0.41` and `12.0.42`.
- **More than one line can be supported at once,** each with its own branch, labels and end date. The work of each fix grows with the number of lines.
