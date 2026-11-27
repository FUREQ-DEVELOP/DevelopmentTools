# Development status

Updated: 2026-11-27 (JST)
Current: PHASE 13 — COMPLETE IN CONSOLIDATED MAIN PR #2 (INDEPENDENT REVIEW PENDING)

## Completed
- PHASE 00: Read-only repository/org/plan/access/submodule/project/actions/billing/environment survey.
- Confirmed FUREQ Workspace had no source/project/tests. DevelopmentTools `main` contained README only.
- PHASE 01 design documents and contributor instructions are included in main PR #2. PHASE 02-13 implementation PRs #3-#14 have been squash-merged in reverse stack order into the PR #2 head branch.
- DevelopmentTools foundation PR #1 is merged to `main` using the permitted squash strategy; the original PR branch is retained because other repositories pin its commit as a submodule.
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
- PHASE 05 checkpoint: Release build succeeded with 0 warnings / 0 errors; all 23 tests passed.
- PHASE 06 reads nested `.gitmodules` and committed gitlinks to show repository, path, pinned SHA, initialization state, and a separate pinned-SHA update preview.
- Recursive initialization is user-confirmed, proceeds one validated level at a time, restricts remotes to FUREQ-DEVELOP HTTPS, uses the committed gitlink SHA, disables hooks and LFS smudge, isolates global/system Git config and templates, and skips already initialized modules.
- PHASE 06 nested local graph test covers recursive initialization, pinned SHA equality, and update preview for a mismatched checkout.
- PHASE 06 checkpoint: Release build succeeded with 0 warnings / 0 errors; all 24 tests passed.
- PHASE 07 adds the versioned 14-repository layout JSON, Full/Rendering/Engine/Game/Shared/Role-based/Custom dependency closure, a read-only Setup Plan, bounded three-worker staged clones, origin/branch/SHA validation, pinned recursive submodule initialization, and final target promotion.
- Schema v3 persists plans and item-level status. Cancellation leaves completed targets intact; the latest saved plan can be reopened and unfinished items retried.
- PHASE 07 local tests cover the 14-target map, dependency closure, local clone execution, cancellation/resume, and SQLite persistence.
- PHASE 07 checkpoint: Release build succeeded with 0 warnings / 0 errors; all 27 tests passed.
- PHASE 08 adds authenticated repository Issue/PR listing, Issue creation, Organization Projects v2 and project-item reading, plus adding a selected Issue to a Project.
- API writes require the app user token and explicit confirmation in the UI; Issue writes are not used in tests. Fake HTTP tests validate payloads, token headers, Projects GraphQL queries/mutations, malformed repository rejection, and timeout no-retry behavior.
- Live Organization Issue/Project reads and writes have not been verified. Issue creation and Project membership updates were tested against fake HTTP only.
- PHASE 08 checkpoint: Release build succeeded with 0 warnings / 0 errors; all 35 tests passed.
- PHASE 10 adds local Markdown listing, bounded full-text search, source editing, known safe templates and path-confirmed atomic saves; the app displays source text and does not render HTML or embedded content.
- PHASE 10 tests cover `.md` filtering, skipped dependency folders, path traversal rejection, source preservation, bounded search and Git-visible diff.
- PHASE 10 checkpoint: Release build succeeded with 0 warnings / 0 errors; all 42 tests passed.
- PHASE 11 adds a local image/LFS pointer inventory for the selected local Git repository and a confirmed GitHub Issue request form for assets.
- Asset scanning skips dependency/build folders and reparse points, caps traversal at 2 GiB and results at 20,000, and reads only small files to identify LFS pointers. It never fetches image thumbnails or LFS payloads.
- Asset request Issues use no assumed repository labels; creation requires explicit confirmation and the existing authenticated collaboration API.
- PHASE 11 tests cover image discovery, dependency-folder exclusion, CRLF LFS pointer metadata, and that pointer files remain unchanged (no object download).
- PHASE 11 checkpoint: Release build succeeded with 0 warnings / 0 errors; all 43 tests passed.
- PHASE 12 adds bounded strict Manifest discovery and confirmation-gated launch for `.exe` files contained in an approved DevelopmentTools checkout. Arbitrary scripts/shell commands and unknown fields are rejected; the current checkout has no manifests, so no tools are offered.
- PHASE 12 Manifest tests cover valid discovery, malformed/unknown-field rejection, path escape rejection, and checkout validation.
- PHASE 12 checkpoint: Release build succeeded with 0 warnings / 0 errors; all 45 tests passed.
- PHASE 13 adds a local view that separates committed Submodule Gitlinks from optional `.fureq/dependencies.json` SDK/API edges, plus confirmed cross-team GitHub Issue requests with optional related-Issue references.
- Dependency declarations are read-only and cannot update Gitlinks. Impact currently means the selected local repository and its discovered dependency graph; a complete cross-repository downstream impact scan is not yet implemented. SDK/API versions come from declarations; live GitHub Release/update detection is not implemented.
- PHASE 13 tests cover SDK/API manifest parsing and Gitlink separation, and reject out-of-organization dependencies.
- Consolidated PHASE 01-13 Release build succeeds with 0 warnings / 0 errors; all 47 tests pass.
- PHASE 09 adds a repeatable self-contained win-x64 single-file ZIP publish script and trial manifest; generated artifact is local and gitignored.
- Published trial executable launched on this Windows account; WPF startup and local DB initialization were observed. This is not a clean-account/clean-machine trial, and live GitHub sign-in was not performed.
- PHASE 09 checkpoint: Release build succeeded with 0 warnings / 0 errors; all 35 tests passed at that phase.

## In progress
- Main PR #2 now contains the Phase 01-13 app and design changes (89 files). Its earlier approval became stale after the stack was consolidated and must be renewed.
- Foundation PR #1 is still open in 13 other organization repositories and awaits independent review; DevelopmentTools PR #1 is merged.
- Repository write access was granted to the existing second organization member `rumo-dev` on all 14 repositories so required reviews can count; only `fureq-dev` and `rumo-dev` are current organization members.
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
PHASE 14-17. Actions/Release/notifications and update/hardening features are not implemented yet.

## Environment constraints
- Current install: VS2026 18.12.1, .NET SDK 10.0.404 and runtime 10.0.15. A .NET 11 SDK/runtime is also present in the machine installation; `global.json` pins the project to .NET 10.
- Visual Studio 2022 is also present but the app targets .NET 10 through Visual Studio 2026.
- DevelopmentTools `main` has the platform foundation. The full Workspace app remains in PR #2 and has not been merged to `main`.
- The 13 foundation PRs outside DevelopmentTools are still awaiting review; several setup branches are pinned by submodules.
- No LFS payload, public Issue, GitHub Release or paid resource created.

## Next
1. Obtain refreshed independent review for main PR #2 and independent reviews for the 13 remaining foundation PRs #1; do not bypass branch protections.
2. After review, merge PR #2 with the repository's allowed squash strategy.
3. Verify Phase 03 Device Flow and authenticated team/repository discovery in the live app.
4. After review/merge, exercise PHASE 05 writes only through the app's confirmation flow and use dedicated/approved targets for any live write verification.
5. Finish PHASE 07 LFS size/selection support and the 14-repository pilot before treating PHASE 07 as complete.
6. Run the ZIP in a clean Windows user profile before treating PHASE 09 as trial-verified.
