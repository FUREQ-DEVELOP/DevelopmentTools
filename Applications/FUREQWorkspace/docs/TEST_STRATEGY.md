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
- API write timeout results in OutcomeUnknown and remote-state check before retry.

## CI
DevelopmentTools is public. Use a small Ubuntu job for restore/build/unit checks where supported and Windows for WPF build/UI verification only as required. Minimize runs, artifacts and duration; no paid service, no LFS payload, least GITHUB_TOKEN permissions. Existing actions allowlist must be respected.
