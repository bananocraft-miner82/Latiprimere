# Releasing

How Latiprimere's packages get to nuget.org. Merging `main` into the `release` branch publishes a release: [release.yml](../.github/workflows/release.yml) chooses the version, makes the release commit, builds and tests it on all three runners, publishes all six packages and tags it. Nothing is typed by hand, not even the version. There is no API key to keep: nuget.org's trusted publishing gives the workflow a key that lasts one hour.

## Versions

Every release is `<.NET major>.0.<build number>`, such as `11.0.42`, for all six packages ([ADR 0028](decisions/0028-automated-releases.md), with the build number from [ADR 0030](decisions/0030-release-numbering.md)):

- **The .NET major** is the newest .NET the packages target, from `<TargetFrameworks>` in [Directory.Build.props](../Directory.Build.props). Today that's `net11.0`, so 11. Adding `net12.0` makes the next release `12.0.<build number>`.
- **The build number** is one more than the latest release's, counting the versions on nuget.org and the `v<version>` tags: after `11.0.5-preview` comes `11.0.6-preview`. A run that fails or is cancelled before publishing uses up nothing, so the numbers have no gaps. It keeps counting across .NET majors: after `11.0.42` would come `12.0.43`. `release.yml` names the trusted publishing policy, so leave its name alone.

[tools/releasing/next-version.sh](../tools/releasing/next-version.sh) works it out from the repository root, with the tags fetched: after `11.0.5-preview`, `sh tools/releasing/next-version.sh` prints `11.0.6`, and with `--preview` it prints `11.0.6-preview`. It needs to reach nuget.org.

### Preview releases

Until manual testing with real printers is confirmed, releases are previews: `11.0.42-preview`. nuget.org lists them as prereleases, so a project only gets one when it asks for prereleases, for example with `--prerelease` or the "Include prerelease" box.

The repository variable `RELEASE_PREVIEW` switches this, under **Settings → Secrets and variables → Actions → Variables**:

- **`true`, or not set:** previews. Not set counts as `true`, so nothing is released as stable by accident.
- **`false`:** stable releases, such as `11.0.43`.

Anything else stops the release before it starts. The change applies from the next release; versions already published keep their suffix. A stable version sorts after every preview with a lower build number, so `11.0.43` comes after `11.0.42-preview`.

The README's status line must agree: [prepare-release.sh](../tools/releasing/prepare-release.sh) stops a preview while it says `stable`, and a stable release while it says `preview`, before anything changes ([check-readme-status.sh](../tools/releasing/check-readme-status.sh)). So the switch is made once, with [switch-to-stable.sh](../tools/releasing/switch-to-stable.sh), as [The first stable release](#the-first-stable-release-10) describes. CI checks these scripts on every push, since they run so rarely: ShellCheck, and [test-release-scripts.sh](../tools/releasing/test-release-scripts.sh), which runs the switch and the status check on scratch copies of the files, with LF and CRLF line endings.

## One-time setup (owner)

1. **nuget.org account.** Sign in at nuget.org with a Microsoft account. Packages belong to this account, or to an organisation it creates.
2. **Reserve the `Latiprimere` ID prefix**, so nobody else can publish `Latiprimere.*` packages and yours get the verified tick. Follow "ID prefix reservation" in the nuget.org documentation: you email the NuGet team with the prefix and your account. This can be done before or after the first release.
3. **Trusted publishing policy.** On nuget.org, open your user name, then **Trusted Publishing**, and add a policy:
   - **Repository owner:** `bananocraft-miner82`
   - **Repository:** `Latiprimere`
   - **Workflow file:** `release.yml`
   - **Environment:** `release`

   For a private repository, the policy stays active for 7 days until the first publish confirms it. A public repository doesn't have this wait.
4. **GitHub environment.** In the repository's **Settings → Environments**, create `release`. Add yourself as a required reviewer if every publish should wait for your approval; without one, merging into `release` is the approval. Under **Deployment branches and tags**, allow the `release` branch only.
5. **The `release` branch.** Create it from `main`. A branch named `release` can't exist beside branches named `release/...`, so delete those first:

   ```bash
   git push origin --delete release/v0.1.1-preview
   git push origin main:refs/heads/release
   ```

   Creating the branch starts the first release, since it is a push to `release`. Then protect it in **Settings → Rules → Rulesets** (or **Branches**): require a pull request, require the three "Build and test" checks to pass, and allow only the **merge** method. So only a merge whose build passed can publish. Squash or rebase merges would give `release` commits that `main` doesn't have, and later merges from `main` could then conflict.

   Each clone that still has `origin/release/v0.1.1-preview` can't fetch the new branch until that is pruned: `git fetch --prune`.
6. **Let workflows open pull requests:** in **Settings → Actions → General → Workflow permissions**, tick **Allow GitHub Actions to create and approve pull requests**. The Release workflow needs it for the post-release pull request.
7. **Repository variable `RELEASE_PREVIEW`** (optional): leave it unset or `true` while releases should be previews, and set it to `false` at the first stable release, once testing with real printers is confirmed ([The first stable release](#the-first-stable-release-10)).
8. **Repository secret `NUGET_USER`:** your nuget.org user name (profile name, not email address). In **Settings → Secrets and variables → Actions**, either as a repository secret or a `release` environment secret.
9. **Update the website automatically.** latiprimere.com reads the changelog and the docs from this repository only when it is built. The "Update website" workflow ([update-website.yml](../.github/workflows/update-website.yml)) rebuilds it after each release has published its packages, and again when the release's post-release pull request is merged. The first rebuild already shows the new version: the website takes its changelog notes from the release's tag while `main` doesn't have them dated. Other merges to `main` don't rebuild it: the site shows the docs of the newest release and only released versions from the changelog, so they wouldn't change anything. It needs a deploy hook:
   1. In the Cloudflare dashboard, go to **Workers & Pages**, select the website's Worker, then **Settings → Builds → Deploy Hooks**.
   2. Name it, for example "Package repository", choose the `release` branch, select **Create**, and copy the URL.
   3. In this repository, add it as the secret `WEBSITE_DEPLOY_HOOK` under **Settings → Secrets and variables → Actions**.

   The URL is the only credential, so keep it out of code and logs. If it leaks, delete the hook in Cloudflare and create a new one. Until the secret is set, the workflow does nothing and says so. To rebuild the site by hand, run "Update website" from the **Actions** tab.

## Each release

1. **On `main`, write the release notes for users** in [CHANGELOG.md](../CHANGELOG.md), under `## Unreleased`. The release dates them under its version, and the website's changelog page shows them. Check that [site/](site/) and [the examples](../samples/Latiprimere.Examples/) cover what the release adds or changes: the website's docs come from the newest release tag, so they go live with it. Add what changed to [progress.md](progress.md), the development log, and tick [plan.md](plan.md).
2. **Open a pull request from `main` into `release`,** in the GitHub UI or with:

   ```bash
   gh pr create --base release --head main --title "Release" --body "Publish what's on main."
   ```

   Its build runs on all three runners.
3. **Merge it** with **Create a merge commit**, once the build passes. Hold other merges into `main` until step 5: a change to `CHANGELOG.md` or a `PublicAPI.Unshipped.txt` in the meantime makes the post-release pull request conflict. Merging starts the Release workflow, which:
   0. **check:** stops, publishing nothing, if the merged commit has exactly the latest release's files ([anything-to-release.sh](../tools/releasing/anything-to-release.sh)). That's what merging only a post-release pull request's commits into `release` gives, as the second one's changes don't count: there's no need to bring `release` up to date with `main` after a release, since the next release's merge brings that commit along.
   1. **prepare:** chooses the version and runs [tools/releasing/prepare-release.sh](../tools/releasing/prepare-release.sh), which:
      - sets the version in [Directory.Build.props](../Directory.Build.props);
      - marks the public API as shipped: each `PublicAPI.Unshipped.txt` moves into `PublicAPI.Shipped.txt`, and `*REMOVED*` entries take their lines out of it;
      - sets `PackageValidationBaselineVersion` to the newest version on nuget.org, so packing fails on a breaking change. `main` already compares with it after the previous release's post-release commit (step 4), so this matters only for a first release. A breaking change in this release needs its `CompatibilitySuppressions.xml`, made by packing with `-p:ApiCompatGenerateSuppressionFile=true`, and an ADR (see [CONTRIBUTING.md](../CONTRIBUTING.md#changing-the-public-api));
      - updates the version in the status line of [README.md](../README.md);
      - turns `## Unreleased` in [CHANGELOG.md](../CHANGELOG.md) into `## <version> - <date>`.

      It commits that as "Release \<version\>" on the branch `post-release/v<version>`. It refuses a version that is already tagged or on nuget.org, and stops if the previous release's post-release pull request hasn't been merged yet (step 5), or if `CHANGELOG.md` changed since the previous release without a `## Unreleased` heading: notes written under a released version's heading would be credited to it.
   2. **build:** [build.yml](../.github/workflows/build.yml) builds, tests and packs the release commit on all three runners.
   3. **publish:** pushes those packages to nuget.org, under the `release` environment. If it requires a reviewer, approve the run in the **Actions** tab.
   4. **finish:** tags the release commit `v<version>`, and waits, up to 30 minutes, until nuget.org serves every package at that version ([tools/releasing/wait-for-packages.sh](../tools/releasing/wait-for-packages.sh)). Then it adds a second commit, "Compare with \<version\>", made by [tools/releasing/post-release.sh](../tools/releasing/post-release.sh). It sets `PackageValidationBaselineVersion` to the version just released, so `main` checks each change against what is on nuget.org now, and removes every `CompatibilitySuppressions.xml`, as their breaking changes are in that version and package validation fails on a suppression it doesn't need. Then it opens the pull request "Post-release \<version\>" into `main`, with both commits. Its build restores the version just published, which is why finish waits for nuget.org first. If the wait times out, finish fails before the second commit; use **Re-run failed jobs** once the packages show on nuget.org.
4. **Check nuget.org.** Each package page shows the README, licence and dependencies. New packages take a few minutes to be indexed and searchable.
5. **Merge "Post-release \<version\>"** into `main`. It brings the release commit's changes back, the dated changelog and the shipped public API, and the second commit's: the new baseline, and the suppressions removed. The next release refuses to start until it is merged.
6. **Check latiprimere.com.** "Update website" rebuilds the site after the release, which then shows the new version's docs, changelog entry and version after a few minutes, and again after the post-release merge.

The script also runs locally from the repository root, for example to see what a release would change: `sh tools/releasing/prepare-release.sh 11.0.99`. It only changes files; it never commits.

**If a run fails or is cancelled before publish,** nothing is published: fix the cause on `main` and release again, which takes the same number. **If it fails during publish or finish,** use **Re-run failed jobs**, which keeps the version; packages that already arrived are skipped. Don't use **Re-run all jobs** once anything is published: it would make a new release commit and stop, because the version is already on nuget.org. Approve a waiting publish within 7 days, while build.yml's packages are kept. Delete the leftover `post-release/v<version>` branch of a release that never published.

A published version can't be deleted from nuget.org, only unlisted. Its version number is used for good, and it keeps its licence.

## The first stable release (1.0)

"1.0" names the first stable release; its version is the next `11.0.<n>` without `-preview` (decision D8 of the [Phase 3 plan](phase-3-plan.md)). [ADR 0028](decisions/0028-automated-releases.md) keeps releases as previews until manual testing with real printers is confirmed, so the sign-off comes first.

### 1. The sign-off

Each check prints only when you say so (decision D9: about ten sheets in all). Record each result in [progress.md](progress.md), with the harness's report.

- [ ] **The HP over IPP** (P3-1, P3-3): every manual check, with PDFs sent as Apple raster, as the HP takes no PDF:

  ```bash
  dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- manual --printer "ipp:ipp://<the HP's address>/ipp/print"
  ```

- [ ] **A refused option fails before the upload** (P3-1): an option the HP doesn't list, such as print quality 6 when it lists 3 to 5, fails at once with IPP status `0x040B`, and nothing prints:

  ```bash
  LATIPRIMERE_IPP_PRINTERS=ipp://<the HP's address>/ipp/print dotnet run --project samples/Latiprimere.Cli -f net11.0 -- print "ipp:ipp://<the HP's address>/ipp/print" tools/test-documents/three-pages.pdf --option ipp:print-quality=6
  ```

- [ ] **The HP over IPPS** (P3-5): first without `--fingerprint`, which fails giving the certificate's fingerprint; check it against the HP's own web page, then run the checks with it pinned:

  ```bash
  dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- manual --printer "ipp:ipps://<the HP's address>:443/ipp/print" --fingerprint <the fingerprint> manual.test-page
  ```

- [ ] **The HP's Windows queue sent over IPP** (P3-4), with the HP's WSD queue added back to Windows:

  ```bash
  dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- manual --printer "windows:HP ENVY 5640 series" manual.windows-queue-over-ipp
  ```

- [ ] **A Mac** (P3-8): the manual checks against a CUPS queue and a printer over direct IPP, and the Phase 0 checklists for [Spike 1](manual-tests/spike-1-macos.md) and [Spike 4](manual-tests/spike-4-network.md). Record them in [manual-tests](manual-tests/README.md).
- [ ] **CI is green** on all three runners, the CUPS integration tests among them.

Anything that fails is fixed and released as a preview first. Then run the checks it touched again.

### 2. The switch

1. **Merge the last preview's post-release pull request,** so `main` has its release commit.
2. **On a branch from `main`,** run:

   ```bash
   sh tools/releasing/switch-to-stable.sh
   ```

   It changes the wording only:
   - the README's and the package README's status say stable;
   - `--prerelease` goes from the install commands;
   - the changelog's notes start with what 1.0 means.

   Read the changelog paragraph against the sign-off: it says the release was checked on paper on a real printer, through the Windows spooler and over IPP and IPPS, and on a Mac. Change it if the sign-off covered less.
3. **Tick plan.md** ("macOS verification on real hardware", "`1.0.0` release", and the open Phase 0 and 3 items the sign-off closes), and add a progress.md entry.
4. **Merge the branch into `main`.**

### 3. The release

1. **Set `RELEASE_PREVIEW` to `false`** ([Preview releases](#preview-releases)). Until then, any release stops, as the README already says stable.
2. **Release as usual** ([Each release](#each-release)): a pull request from `main` into `release`. It publishes `11.0.<n>`, which nuget.org lists as the latest stable version.
3. **Merge its post-release pull request.**
4. **latiprimere.com** rebuilds from the release's docs and changelog. Its own pages, in the website's repository, may still call Latiprimere a preview: change them there.
5. **Leave `RELEASE_PREVIEW` at `false`:** previews end with 1.0.

## Before the first release only

- **Check that `sales@latiprimere.com` receives mail.** It's the contact in [COMMERCIAL.md](../COMMERCIAL.md), which the licence in every package points to.

## What's in a package

- **The library:** for `net10.0` and `net11.0`, with XML documentation.
- **`LICENSE`:** the split licence isn't an SPDX licence, so the file itself goes in ([ADR 0015](decisions/0015-split-licence.md)).
- **`README.md`:** from [src/PACKAGE-README.md](../src/PACKAGE-README.md), the same for all six packages. Links in it must be absolute, since nuget.org can't resolve paths in the repository.
- **`icon.png`:** the Latiprimere logo from [src/icon.png](../src/icon.png), 128×128 on a white disc so it shows on dark themes too. 0.1.0-preview shipped without it.
- **Symbols:** in a `.snupkg` beside each package, published with it, with Source Link to the commit on GitHub.
- **Checked when packed:** package validation compares the target frameworks with each other, and the package with the previous release.

`build.yml` packs on the Ubuntu runner for every change and keeps the packages for 7 days as a `packages` artifact, so they can be inspected before a release. In a release, those are the packages that are published.
