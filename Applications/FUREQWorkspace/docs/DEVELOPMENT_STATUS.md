# Development status

Updated: 2026-11-27 (JST)
Current: PHASE 07 — IMPLEMENTED WITH OPEN ITEMS (PR #8 open)

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
- PHASE 05 adds safe Git status/branch creation/fetch/fast-forward-only pull/staged commit/push and pull request creation.
- Git commands use ArgumentList, explicit working directory, bounded async output, timeout/cancellation, disabled hooks and Git Credential Manager only for network operations. Push checks the signed-in GitHub account against the GCM username and refuses protected branches.
- Git origins must be HTTPS `github.com/FUREQ-DEVELOP`; pull requires a clean worktree and an `origin/*` upstream. Writes require explicit UI confirmation.
- PHASE 05 tests cover local bare-remote workflows, fast-forward and dirty-pull behavior, hook suppression, URL allowlisting, identity matching, and PR request/timeout behavior.
- Current Release build succeeds with 0 warnings / 0 errors; all 23 tests pass.
- PHASE 06 reads nested `.gitmodules` and committed gitlinks to show repository, path, pinned SHA, initialization state, and a separate pinned-SHA update preview.
- Recursive initialization is user-confirmed, proceeds one validated level at a time, restricts remotes to FUREQ-DEVELOP HTTPS, uses the committed gitlink SHA, disables hooks and LFS smudge, isolates global/system Git config and templates, and skips already initialized modules.
- PHASE 06 nested local graph test covers recursive initialization, pinned SHA equality, and update preview for a mismatched checkout.
- Current Release build succeeds with 0 warnings / 0 errors; all 24 tests pass.
- PHASE 07 adds the versioned 14-repository layout JSON, Full/Rendering/Engine/Game/Shared/Role-based/Custom dependency closure, a read-only Setup Plan, bounded three-worker staged clones, origin/branch/SHA validation, pinned recursive submodule initialization, and final target promotion.
- Schema v3 persists plans and item-level status. Cancellation leaves completed targets intact; the latest saved plan can be reopened and unfinished items retried.
- PHASE 07 local tests cover the 14-target map, dependency closure, local clone execution, cancellation/resume, and SQLite persistence.
- Current Release build succeeds with 0 warnings / 0 errors; all 27 tests pass.

## In progress
- Phase 01 PR #2, Phase 02 PR #3, Phase 03 PR #4, Phase 04 PR #5, Phase 05 PR #6, and Phase 06 PR #7 are open and awaiting independent review.
- Phase 07 PR #8 is open and awaiting review.
- PHASE 06 submodule operations were tested only with nested local repositories; no organization repository was initialized or updated.
- PHASE 07 Lightweight/Complete/Custom LFS modes are deliberately blocked pending payload-size estimation and path selection. Only Development mode (skip LFS) executes.
- The live clean-clone check for all 14 public repositories and team pilot have not run; no organization repositories were cloned by this change.
- PHASE 03 Device Flow and Windows Credential Manager persistence are not yet verified against live user authorization.
- PHASE 05 GitHub push and PR creation were not exercised against the live organization; only local Git and fake-HTTP tests ran.

## Not started
PHASE 08-17. Issues/Projects workflow, CI, and release artifacts do not exist yet. PHASE 03 live sign-in and authenticated data verification remain incomplete.

## Environment constraints
- Current install: VS2026 18.12.1, .NET SDK 10.0.404 and runtime 10.0.15. A .NET 11 SDK/runtime is also present in the machine installation; `global.json` pins the project to .NET 10.
- Visual Studio 2022 is also present but the app targets .NET 10 through Visual Studio 2026.
- DevelopmentTools `main` only has README; setup PR #1 is still open/review-required. This feature uses a separate branch from main.
- Full org setup PRs and Submodule changes are not merged; use live GitHub discovery and `.gitmodules`, not assumed PR state.
- No LFS payload, public Issue, GitHub Release or paid resource created.

## Next
1. Obtain independent reviews for PHASE 01-07 PRs #2-#8 in dependency order; merge protections must not be bypassed.
2. Verify Phase 03 Device Flow and authenticated team/repository discovery in the live app.
3. Review and merge PR #2-#8 in dependency order, without bypassing branch protections.
4. Verify PHASE 03 live sign-in and, after review/merge, exercise PHASE 05 writes only through the app's confirmation flow.
5. Finish PHASE 07 LFS size/selection support and the 14-repository pilot before treating PHASE 07 as complete.
