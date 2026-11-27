# Development status

Updated: 2026-11-27 (JST)
Current: PHASE 04 — COMPLETED (PR #5 open; review pending)

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
- PHASE 04 schema v2 adds workspace profiles and local repository inventory; duplicate canonical paths are rejected and re-scanning is idempotent.
- PHASE 04 inventory examines only immediate child folders, skips reparse points, and checks only for a `.git` marker without reading config or running Git.
- PHASE 04 tests cover v1-to-v2 data-preserving migration, duplicate profile paths, immediate-only discovery, repeat scans, and missing-folder status.
- Current Release build succeeds with 0 warnings / 0 errors; all 10 tests pass.

## In progress
- Phase 01 PR #2, Phase 02 PR #3, Phase 03 PR #4, and Phase 04 PR #5 are open and awaiting independent review.
- PHASE 03 Device Flow and Windows Credential Manager persistence are not yet verified against live user authorization.

## Not started
PHASE 05-17. Git operations, setup workflow, CI, and release artifacts do not exist yet. PHASE 03 live sign-in and authenticated data verification remain incomplete.

## Environment constraints
- Current install: VS2026 18.12.1, .NET SDK 10.0.404 and runtime 10.0.15. A .NET 11 SDK/runtime is also present in the machine installation; `global.json` pins the project to .NET 10.
- Visual Studio 2022 is also present but the app targets .NET 10 through Visual Studio 2026.
- DevelopmentTools `main` only has README; setup PR #1 is still open/review-required. This feature uses a separate branch from main.
- Full org setup PRs and Submodule changes are not merged; use live GitHub discovery and `.gitmodules`, not assumed PR state.
- No LFS payload, public Issue, GitHub Release or paid resource created.

## Next
1. Obtain independent reviews for Phase 01-04 PRs #2-#5; merge protections must not be bypassed.
2. Verify Phase 03 Device Flow and authenticated team/repository discovery in the live app.
3. Start Phase 05 Git operations using the safe process-runner and repository-identity rules.
