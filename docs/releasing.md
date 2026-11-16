# Releasing

How Latiprimere's packages get to nuget.org. The "Prepare release" workflow opens a pull request with the release commit. After it is merged, pushing a version tag runs [release.yml](../.github/workflows/release.yml), which builds, tests, packs and publishes all six packages. There is no API key to keep: nuget.org's trusted publishing gives the workflow a key that lasts one hour.

## One-time setup (owner)

1. **nuget.org account.** Sign in at nuget.org with a Microsoft account. Packages belong to this account, or to an organisation it creates.
2. **Reserve the `Latiprimere` ID prefix**, so nobody else can publish `Latiprimere.*` packages and yours get the verified tick. Follow "ID prefix reservation" in the nuget.org documentation: you email the NuGet team with the prefix and your account. This can be done before or after the first release.
3. **Trusted publishing policy.** On nuget.org, open your user name, then **Trusted Publishing**, and add a policy:
   - **Repository owner:** `bananocraft-miner82`
   - **Repository:** `Latiprimere`
   - **Workflow file:** `release.yml`
   - **Environment:** `release`

   For a private repository, the policy stays active for 7 days until the first publish confirms it. A public repository doesn't have this wait.
4. **GitHub environment.** In the repository's **Settings → Environments**, create `release`. Add yourself as a required reviewer, so every publish waits for your approval. Limit it to tags matching `v*` if you like.
5. **Let workflows open pull requests:** in **Settings → Actions → General → Workflow permissions**, tick **Allow GitHub Actions to create and approve pull requests**. "Prepare release" needs it.
6. **Repository secret `NUGET_USER`:** your nuget.org user name (profile name, not email address). In **Settings → Secrets and variables → Actions**, either as a repository secret or a `release` environment secret.
7. **Update the website automatically.** latiprimere.com reads the changelog and the docs from this repository only when it is built. The "Update website" workflow ([update-website.yml](../.github/workflows/update-website.yml)) rebuilds it when `CHANGELOG.md` or `docs/site/` changes on `main`, and after each release has published its packages. It needs a deploy hook:
   1. In the Cloudflare dashboard, go to **Workers & Pages**, select the website's Worker, then **Settings → Builds → Deploy Hooks**.
   2. Name it, for example "Package repository", choose the `release` branch, select **Create**, and copy the URL.
   3. In this repository, add it as the secret `WEBSITE_DEPLOY_HOOK` under **Settings → Secrets and variables → Actions**.

   The URL is the only credential, so keep it out of code and logs. If it leaks, delete the hook in Cloudflare and create a new one. Until the secret is set, the workflow does nothing and says so. To rebuild the site by hand, run "Update website" from the **Actions** tab.

## Each release

1. **Check that `main` is ready:** the build is green on all three runners.
2. **Run "Prepare release"** from the repository's **Actions** tab, on `main`, with the version, such as `0.1.0-preview`. Previews after the first are `0.1.0-preview.2`, `0.1.0-preview.3` and so on, which sort after `0.1.0-preview`.

   The workflow ([prepare-release.yml](../.github/workflows/prepare-release.yml)) runs [tools/releasing/prepare-release.sh](../tools/releasing/prepare-release.sh), which:
   - sets the version in [Directory.Build.props](../Directory.Build.props);
   - marks the public API as shipped: each `PublicAPI.Unshipped.txt` moves into `PublicAPI.Shipped.txt`, and `*REMOVED*` entries take their lines out of it;
   - from the second release on, sets `PackageValidationBaselineVersion` to the previous release, so packing fails on a breaking change. Breaking changes are allowed below 1.0, but should be deliberate;
   - updates the status line in [README.md](../README.md).

   It then checks that everything builds and packs, and opens a pull request from `release/v<version>`. Its build runs on all three runners. The script refuses a version that is already tagged.
3. **On the pull request's branch,** write the release notes for users in [CHANGELOG.md](../CHANGELOG.md): move the `## Unreleased` notes under a `## <version> - <yyyy-mm-dd>` heading. The website's changelog page shows them. Check that [site/](site/) and [the examples](../samples/Latiprimere.Examples/) cover what the release adds or changes: the website's docs come from the newest release tag, so they go live with it. Then add what changed to [progress.md](progress.md), the development log, and tick [plan.md](plan.md). That needs judgement, so it isn't automated.
4. **Merge the pull request, then tag the merge commit** and push the tag. The pull request's description has the commands:

   ```bash
   git switch main && git pull
   git tag v0.1.0-preview
   git push origin v0.1.0-preview
   ```

5. **Approve the run** in the repository's **Actions** tab, under the `release` environment.
6. **Check nuget.org.** Each package page shows the README, licence and dependencies. New packages take a few minutes to be indexed and searchable.
7. **Check latiprimere.com.** Once the release has published, "Update website" rebuilds the site, which then shows the new version's docs and changelog entry after a few minutes.

The script also runs locally from the repository root, for example to see what a release would change: `sh tools/releasing/prepare-release.sh 0.1.0-preview`. It only changes files; it never commits.

If the run fails before **Push**, nothing is published: fix it, delete the tag, and tag again. If it fails during **Push**, re-run it; packages that already arrived are skipped.

A published version can't be deleted from nuget.org, only unlisted. Its version number is used for good, and it keeps its licence.

## Before the first release only

- **Check that `sales@latiprimere.com` receives mail.** It's the contact in [COMMERCIAL.md](../COMMERCIAL.md), which the licence in every package points to.

## What's in a package

- **The library:** for `net10.0` and `net11.0`, with XML documentation.
- **`LICENSE`:** the split licence isn't an SPDX licence, so the file itself goes in ([ADR 0015](decisions/0015-split-licence.md)).
- **`README.md`:** from [src/PACKAGE-README.md](../src/PACKAGE-README.md), the same for all six packages. Links in it must be absolute, since nuget.org can't resolve paths in the repository.
- **`icon.png`:** the Latiprimere logo from [src/icon.png](../src/icon.png), 128×128 on a white disc so it shows on dark themes too. 0.1.0-preview shipped without it.
- **Symbols:** in a `.snupkg` beside each package, published with it, with Source Link to the commit on GitHub.
- **Checked when packed:** package validation compares the target frameworks with each other.

`build.yml` packs on the Ubuntu runner for every change and keeps the packages for 7 days as a `packages` artifact, so they can be inspected before a release.
