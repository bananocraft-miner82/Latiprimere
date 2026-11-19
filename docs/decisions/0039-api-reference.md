# 0039: API reference generated into committed pages

- Status: Accepted
- Date: 2026-11-19

## Context

Phase 3's plan asks for an API reference generated from the XML documentation (P3-7, decision D7: "DocFX, or the site's own generator"). latiprimere.com is an Astro Starlight site in its own repository, built by Cloudflare Workers Builds. It reads `docs/site` and the changelog from this repository's newest release tag when it is built, and the "Update website" workflow rebuilds it after each release. So the reference has to be in the tagged commit, or published with the release.

Starlight has no .NET generator, and every page needs a `title` in its front matter, which DocFX's Markdown output doesn't add. Generating during the website build would mean installing .NET there and building this repository from the website's.

Owner: "Yes, committed pages".

## Decision

- **A generator of our own,** `tools/api-reference`: a console program that references the six packages, reads their public types by reflection and their XML documentation files, and writes Starlight Markdown. About 1,200 lines besides its documentation comments, where DocFX would need configuration and a post-processing step for the front matter.
- **Committed pages** in `docs/site/reference/`: an index of every package and its types, and a page per public type in a folder per package, with its declaration, documentation, and each member's declaration, parameters, return value and exceptions. Each release tag carries the reference for its own API, and the website reads it like any other page.
- **Checked in CI,** like the `PublicAPI.*.txt` files: the Linux build regenerates the pages and fails if they differ from what's committed. Contributors run `dotnet run --project tools/api-reference` after changing a public type or its comments (CONTRIBUTING.md).
- **What a page shows:** public members the type declares, without what the compiler adds (records' `Equals`, accessors); `<inheritdoc/>` filled in from the base type or interface; links to other reference pages and members, and to Microsoft Learn for .NET's own types; C# declarations with nullable annotations, default values and `this` for extension methods.
- **Output is the same everywhere:** LF line endings, UTF-8 without a BOM, everything sorted, numbers and defaults written invariantly.
- **Not done:** inherited members aren't repeated on derived types' pages; protected members aren't shown.

## Consequences

- **Public comments are published as they are,** so they're written for the packages' users: no ADR numbers or plan references. The one there was, on `IWindowsPrinter`, is removed.
- **The website** gets a `reference` folder under the docs. Its sidebar needs to include it, if it doesn't pick up new folders by itself.
- **Tests:** `tests/Latiprimere.ApiReference.Tests` checks that every public type's and member's documentation ID matches the compiler's, the Markdown each documentation tag becomes, the declarations, and that two runs write the same pages.
- **P3-9's API review** can read the whole public API in one place.
