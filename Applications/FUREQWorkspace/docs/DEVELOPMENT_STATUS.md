# Development status

Updated: 2026-11-27 (JST)
Current: PHASE 13 — DEPENDENCY GRAPH AND TEAM REQUESTS IN PROGRESS

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
- PHASE 08 adds authenticated repository Issue/PR listing, Issue creation, Organization Projects v2 and project-item reading, plus adding a selected Issue to a Project.
- API writes require the app user token and explicit confirmation in the UI; Issue writes are not used in tests. Fake HTTP tests validate payloads, token headers, Projects GraphQL queries/mutations, malformed repository rejection, and timeout no-retry behavior.
- Live Organization Issue/Project reads and writes have not been verified. Issue creation and Project membership updates were tested against fake HTTP only.
- Current Release build succeeds with 0 warnings / 0 errors; all 35 tests pass.
- PHASE 10 adds local Markdown listing, bounded full-text search, source editing, known safe templates and path-confirmed atomic saves; the app displays source text and does not render HTML or embedded content.
- PHASE 10 tests cover `.md` filtering, skipped dependency folders, path traversal rejection, source preservation, bounded search and Git-visible diff.
- Current Release build succeeds with 0 warnings / 0 errors; all 42 tests pass.
- PHASE 11 adds a local image/LFS pointer inventory for the selected local Git repository and a confirmed GitHub Issue request form for assets.
- Asset scanning skips dependency/build folders and reparse points, caps traversal at 2 GiB and results at 20,000, and reads only small files to identify LFS pointers. It never fetches image thumbnails or LFS payloads.
- Asset request Issues use no assumed repository labels; creation requires explicit confirmation and the existing authenticated collaboration API.
- PHASE 11 tests cover image discovery, dependency-folder exclusion, CRLF LFS pointer metadata, and that pointer files remain unchanged (no object download).
- Current Release build succeeds with 0 warnings / 0 errors; all 43 tests pass.
- PHASE 12 adds bounded strict Manifest discovery and confirmation-gated launch for `.exe` files contained in an approved DevelopmentTools checkout. Arbitrary scripts/shell commands and unknown fields are rejected; the current checkout has no manifests, so no tools are offered.
- PHASE 12 Manifest tests cover valid discovery, malformed/unknown-field rejection, path escape rejection, and checkout validation.
- Current Release build succeeds with 0 warnings / 0 errors; all 45 tests pass.
- PHASE 13 adds a local view that separates committed Submodule Gitlinks from optional `.fureq/dependencies.json` SDK/API edges, plus confirmed cross-team GitHub Issue requests with optional related-Issue references.
- Dependency declarations are read-only and cannot update Gitlinks. Impact currently means the selected local repository and its discovered dependency graph; a complete cross-repository downstream impact scan is not yet implemented. SDK/API versions come from declarations; live GitHub Release/update detection is not implemented.
- PHASE 13 tests cover SDK/API manifest parsing and Gitlink separation, and reject out-of-organization dependencies.
- Current Release build succeeds with 0 warnings / 0 errors; all 47 tests pass.
- PHASE 09 adds a repeatable self-contained win-x64 single-file ZIP publish script and trial manifest; generated artifact is local and gitignored.
- Published trial executable launched on this Windows account; WPF startup and local DB initialization were observed. This is not a clean-account/clean-machine trial, and live GitHub sign-in was not performed.
- Current Release build succeeds with 0 warnings / 0 errors; all 35 tests pass.

## In progress
- Phase 01 PR #2, Phase 02 PR #3, Phase 03 PR #4, Phase 04 PR #5, Phase 05 PR #6, and Phase 06 PR #7 are open and awaiting independent review.
- Phase 07 PR #8 is open and awaiting review.
- Phase 08 PR #9 is open and awaiting review.
- PHASE 09 PR #10 is open and awaiting review.
- PHASE 10 PR #11 is open and awaiting review.
- PHASE 11 PR #12 is open and awaiting review.
- PHASE 12 PR #13 is open and awaiting review.
- PHASE 13 implementation is tested; PR creation pending.
- PHASE 09 trial ZIP is not a formal release; it has not been uploaded or distributed. Clean Windows user/account trial remains pending.
- PHASE 10 markdown viewer/editor is local-only and has not been visually checked in a clean user profile; save/update behavior is covered by local filesystem and Git tests.
- PHASE 11 asset inventory has not been visually checked in the WPF app; live Issue creation and LFS payload access were not exercised.
- PHASE 12 tool execution is not exercised because the checkout has no registered tool; UI visual check remains pending.
- PHASE 07 LFS estimates/selection and live 14-repository pilot remain open validation items.
- PHASE 06 submodule operations were tested only with nested local repositories; no organization repository was initialized or updated.
- PHASE 07 Lightweight/Complete/Custom LFS modes are deliberately blocked pending payload-size estimation and path selection. Only Development mode (skip LFS) executes.
- The live clean-clone check for all 14 public repositories and team pilot have not run; no organization repositories were cloned by this change.
- PHASE 03 Device Flow and Windows Credential Manager persistence are not yet verified against live user authorization.
- PHASE 05 GitHub push and PR creation were not exercised against the live organization; only local Git and fake-HTTP tests ran.

## Not started
PHASE 14-17. Actions/Release/notifications and update/hardening features are not implemented yet. PHASE 03 live sign-in and authenticated data verification remain incomplete.

## Environment constraints
- Current install: VS2026 18.12.1, .NET SDK 10.0.404 and runtime 10.0.15. A .NET 11 SDK/runtime is also present in the machine installation; `global.json` pins the project to .NET 10.
- Visual Studio 2022 is also present but the app targets .NET 10 through Visual Studio 2026.
- DevelopmentTools `main` only has README; setup PR #1 is still open/review-required. This feature uses a separate branch from main.
- Full org setup PRs and Submodule changes are not merged; use live GitHub discovery and `.gitmodules`, not assumed PR state.
- No LFS payload, public Issue, GitHub Release or paid resource created.

## Next
1. Obtain independent reviews for PHASE 01-13 PRs #2-#14 in dependency order; merge protections must not be bypassed.
2. Verify Phase 03 Device Flow and authenticated team/repository discovery in the live app.
3. Review and merge PR #2-#14 in dependency order, without bypassing branch protections.
4. Verify PHASE 03 live sign-in and, after review/merge, exercise PHASE 05 writes only through the app's confirmation flow.
5. Review/merge PHASE 08 PR #9 after creation; verify live read access, then use only dedicated/approved targets for any live write verification.
6. Finish PHASE 07 LFS size/selection support and the 14-repository pilot before treating PHASE 07 as complete.
7. Review/merge PHASE 09 PR #10, then run the ZIP in a clean Windows user profile before treating the MVP as trial-verified.
8. Review/merge PHASE 10 PR #11, then PR #12 (assets/LFS) and #13 (tools).
9. Implement and review PHASE 13 dependency graph and cross-team requests.
