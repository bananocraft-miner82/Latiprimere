# Latiprimere Documentation

| Document | Purpose |
|---|---|
| [progress.md](progress.md) | **Start here.** Current status and dated progress log |
| [site/](site/) | The developer documentation published at [latiprimere.com/docs](https://latiprimere.com/docs/). The website copies these pages in at build time; the sidebar follows the folders and each page's `sidebar.order` |
| [plan.md](plan.md) | Vision, scope, architecture, phases, risks, open questions |
| [poc-plan.md](poc-plan.md) | Phase 0 proof-of-concept spikes and go/no-go criteria |
| [go-no-go.md](go-no-go.md) | Phase 0 go/no-go review: results against the criteria, and the recommendation |
| [api-review-0.1.md](api-review-0.1.md) | Review of the public API before the first preview package: decisions for the owner, additive changes, documentation fixes |
| [releasing.md](releasing.md) | How packages are published to nuget.org: one-time setup and the steps for each release |
| [feasibility.md](feasibility.md) | Market and technical feasibility research, with sources |
| [decisions/](decisions/README.md) | Architecture decision records |
| [manual-tests/](manual-tests/) | Step-by-step checks the owner runs on hardware CI can't cover, such as macOS |

## Keeping these up to date

- After each working session, add an entry to the top of [progress.md](progress.md) and update its "Current status" table.
- Tick milestone checkboxes in [plan.md](plan.md) as they complete.
- Record significant choices as a new ADR in [decisions/](decisions/README.md) rather than editing accepted ones.
- The code in [site/](site/) is copied from [samples/Latiprimere.Examples](../samples/Latiprimere.Examples/), which the build compiles. Change the example first, then the page, so the docs never show code that doesn't build.
