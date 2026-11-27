# Development status

Updated: 2026-11-27 (JST)
Current: PHASE 01 — DESIGNING (local draft; PR not yet reviewed)

## Completed
- PHASE 00: Read-only repository/org/plan/access/submodule/project/actions/billing/environment survey.
- Confirmed FUREQ Workspace has no existing source/project/tests. DevelopmentTools `main` currently contains README only.
- PHASE 01 requirements, architecture, UI, API, security, data, Setup, tool, test, plan and contributor instructions drafted locally.
- User approved .NET 10 LTS + Visual Studio 2026 baseline and GitHub App user Device Flow direction.

## In progress
- Complete document consistency review, then submit the Phase 01 PR for independent review.
- Register GitHub App, enable Device Flow, install only on 14 initial repositories with the approved minimum permissions. User approved; owner action/configuration still pending.
- Update local Visual Studio/.NET servicing SDK to current supported .NET 10 band. User approved baseline; installation has not yet been run.

## Not started
PHASE 02-17. No solution, project code, test, workflow or release artifact exists yet.

## Environment constraints
- Current install: VS2026 18.7.3, .NET SDK 10.0.301 and runtime 10.0.9; latest checked .NET 10 patch is 10.0.15. Update before relying on build/servicing.
- Visual Studio 2022 17.14.17 is present but does not officially target net10.
- DevelopmentTools `main` only has README; setup PR #1 is still open/review-required. This feature uses a separate branch from main.
- Full org setup PRs and Submodule changes are not merged; use live GitHub discovery and `.gitmodules`, not assumed PR state.
- No LFS payload, public Issue, GitHub Release or paid resource created.

## Next
1. Complete Phase 01 docs/consistency and submit PR.
2. Complete GitHub App setup and machine SDK update.
3. Begin Phase 02 only after design PR review.
