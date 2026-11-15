# Latiprimere Documentation

| Document | Purpose |
|---|---|
| [progress.md](progress.md) | **Start here.** Current status and dated progress log |
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
