# 0030: Number releases after the latest one

- Status: Accepted
- Date: 2026-11-18

## Context

[ADR 0028](0028-automated-releases.md) made a release's build number the Release workflow's run number. Every run uses one up, including runs that publish nothing.

After 11.0.3-preview, pull request #6 merged `main` into `release` to bring it up to date. `main` had nothing new but the post-release commit, so the merged commit had exactly the files of `v11.0.3-preview`. That push started run 4, which made a `post-release/v11.0.4-preview` branch and was then cancelled. The next real release was run 5, so it went out as 11.0.5-preview and 11.0.4 never existed.

Owner: "the release process is incrementing the version number twice rather than once".

The same release showed a second gap. Its changelog notes had been added under the released `## 11.0.3-preview` heading rather than under `## Unreleased`. So 11.0.5-preview went out without an entry of its own. 11.0.3's entry claimed changes it didn't ship. And the next release would have refused to start, because its check for the previous release's dated heading found none.

## Decision

- **The build number is one more than the latest release's,** the highest build number among the versions on nuget.org and the `v<version>` tags, whatever their .NET major. Versions from before ADR 0028, such as `0.1.0-preview`, don't count. [next-version.sh](../../tools/releasing/next-version.sh) works it out, so a failed or cancelled run uses nothing up and a re-run gets the same number. Tags count as well as nuget.org because nuget.org can take minutes to list a new version. nuget.org counts as well as tags because a release can publish and then fail before it is tagged.
- **A push with nothing new publishes nothing.** The workflow's first job, `check`, compares the merged commit's files with the latest release tag's ([anything-to-release.sh](../../tools/releasing/anything-to-release.sh)). If they are the same, it stops there and the other jobs are skipped.
- **"Post-release merged" is checked through the tag, not the changelog.** [prepare-release.sh](../../tools/releasing/prepare-release.sh) requires `v<previous>` to be in the merged commit's history. A release without notes no longer blocks the next one.
- **Notes under a released heading stop the release.** If `CHANGELOG.md` changed since the previous release's tag and has no `## Unreleased` heading, prepare-release.sh refuses, and says where the notes belong.
- **The changelog is corrected:** the three changes that shipped in 11.0.5-preview move from 11.0.3's entry to an entry of their own.

## Consequences

- **The numbers have no gaps.** The next release is 11.0.6-preview.
- **The run number no longer matters,** so renaming `release.yml` wouldn't restart the count. It still names nuget.org's trusted publishing policy, so it keeps its name.
- **Two runs must never prepare at once,** or both would choose the same number. The workflow's `concurrency` group already runs one release at a time. prepare-release.sh refuses a version already tagged or on nuget.org, so a race would fail rather than publish twice.
- **The scripts need the tags.** The workflow's checkouts already fetch all history and tags. Locally, run `git fetch --tags` first.
- **Run 4's leftover branch,** `post-release/v11.0.4-preview`, can be deleted, as [releasing.md](../releasing.md) says for any release that never published.
