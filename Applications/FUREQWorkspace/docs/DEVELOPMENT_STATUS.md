# Development status

Updated: 2026-11-27 (JST)
Current: PHASE 03 — IN PROGRESS (Device Flow and discovery implemented; live sign-in not yet verified)

## Completed
- PHASE 00: Read-only repository/org/plan/access/submodule/project/actions/billing/environment survey.
- Confirmed FUREQ Workspace had no source/project/tests. DevelopmentTools `main` contained README only.
- PHASE 01 design documents and contributor instructions committed as `93f160d`; PR #2 is open and awaiting independent review.
- GitHub App `FUREQ Workspace` registered with Device Flow and installed on exactly the initial 14 selected repositories. Site was excluded. One App private key remains because GitHub does not allow deleting the last key.
- Visual Studio Community 2026 updated to 18.12.1. .NET SDK 10.0.404 and runtime 10.0.15 are installed; the application pins SDK 10.0.404.
- PHASE 02 initial host, navigation, debug logging, SQLite schema v1/settings repository, and migration test implemented locally.
- PHASE 02 Release build succeeded with 0 warnings / 0 errors.
- PHASE 02 test: SQLite migration idempotency and settings round-trip passed (1/1).
- PHASE 02 WPF GUI started; home screen, DB initialization message and navigation to Repositories were observed.
- PHASE 03 public guest discovery implemented against GitHub REST; results are paginated and displayed read-only.
- PHASE 03 pagination test passes using a fake HTTP handler; a real unauthenticated request returned 15 public organization repositories.
- PHASE 03 Device Flow, token refresh, local sign-out, Windows Credential Manager storage, and authenticated user/team/repository discovery implemented.
- PHASE 03 fake-handler tests cover token storage boundary, cancellation, slow-down polling, pagination, and Bearer header use.

## In progress
- Phase 01 PR #2 and Phase 02 PR #3 are open and awaiting independent review.
- Verify PHASE 03 Device Flow in the live app with the user; verify the authenticated repository and team results.

## Not started
PHASE 04-17. Workspace profiles, Git operations, setup workflow, CI, and release artifacts do not exist yet. PHASE 03 live sign-in and authenticated data verification remain incomplete.

## Environment constraints
- Current install: VS2026 18.12.1, .NET SDK 10.0.404 and runtime 10.0.15. A .NET 11 SDK/runtime is also present in the machine installation; `global.json` pins the project to .NET 10.
- Visual Studio 2022 is also present but the app targets .NET 10 through Visual Studio 2026.
- DevelopmentTools `main` only has README; setup PR #1 is still open/review-required. This feature uses a separate branch from main.
- Full org setup PRs and Submodule changes are not merged; use live GitHub discovery and `.gitmodules`, not assumed PR state.
- No LFS payload, public Issue, GitHub Release or paid resource created.

## Next
1. Obtain independent reviews for Phase 01 PR #2 and Phase 02 PR #3; merge protections must not be bypassed.
2. Verify Phase 03 Device Flow and authenticated team/repository discovery in the live app.
