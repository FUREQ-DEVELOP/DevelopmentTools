# Test strategy

## Layers
- Unit: path/repository identity validation, setup profile/dependency closure, state transitions, Git porcelain and `.gitmodules` parsers, LFS mode selection, pagination/rate-limit and error mapping.
- Integration: temporary local Git repositories with direct/nested submodules; clone/fetch/dirty/branch conflicts/cancel/resume; SQLite migrations; no new GitHub repository.
- API: fake `HttpMessageHandler` by default. Real GitHub calls require explicit opt-in and target only read-only/public endpoints unless a disposable issue is specifically approved. Never test writes against production Issues/PRs.
- UI: WPF startup, wizard progression, validation/errors, high DPI, keyboard navigation, high contrast. Separate build success from actual GUI test.

## Required safety cases
- Existing/non-empty directory and remote mismatch cause no overwrite.
- Dirty, detached, conflict and unexpected remote block automatic pull.
- Cancellation preserves completed repositories and offers resume.
- Token/credential never appears in logs, args, URLs or DB.
- Submodule gitlink remains pinned unless user explicitly selects an update.
- Nested `.gitmodules` paths and URLs are validated before recursive submodule initialization; initialization uses the committed gitlink SHA and update-preview inspection never changes the checkout.
- Setup plan tests cover versioned 14-repository mapping, dependency closure, bounded local clone execution, staging promotion, cancellation, resume, and persisted per-item status. Live 14-repository clone/pilot remains a separate opt-in verification.
- Collaboration API tests use fake HTTP only; assert token headers, typed payloads, issue-vs-PR filtering, Project v2 query/mutation, bounded pages, malformed target rejection and no retry on timed-out writes. No live production issue/project write is part of testing.
- Markdown tests cover repository-contained paths, `.md` filtering, excluded directories, source preservation (including HTML-like text without execution), bounded search, atomic save and Git-visible diff.
- Asset tests cover image discovery, excluded dependency folders, CRLF Git LFS pointer metadata, and confirm pointer files remain unchanged; tests never fetch LFS objects or create live Issues.
- Tool-manifest tests cover strict JSON, supported schema/type, malformed and unknown fields, out-of-checkout paths, and approved-checkout validation; tests do not execute arbitrary tools.
- Dependency tests cover the separation of declared SDK/API edges from Gitlinks and reject out-of-organization dependencies; cross-team Issue creation remains fake-HTTP-covered only and has no live write check.
- API write timeout results in OutcomeUnknown and remote-state check before retry.

## CI
DevelopmentTools is public. Use a small Ubuntu job for restore/build/unit checks where supported and Windows for WPF build/UI verification only as required. Minimize runs, artifacts and duration; no paid service, no LFS payload, least GITHUB_TOKEN permissions. Existing actions allowlist must be respected.
- Trial packaging publishes the app project only as a self-contained win-x64 single-file ZIP, then verifies startup using a clean Windows account. Do not upload/distribute the trial from CI without separate approval.

## Initial automated checks
- .NET 10 uses Microsoft Testing Platform for `dotnet test`; `global.json` selects that runner.
- Phase 02 verifies SQLite schema migration repeatability and AppSettings round-trip with a temporary database.
