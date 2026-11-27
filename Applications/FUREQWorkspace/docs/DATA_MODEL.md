# ローカルデータモデル

SQLiteは端末内の操作状態/キャッシュのみ。GitHubがIssues、Projects、repository、membershipの正本。全表にschema migration versionを持ち、個人ユーザーのLocalAppData配下に保存。

## MVP tables
- `AppSettings`: locale/theme/parallel limit/root path/LFS defaults. No token or secret columns.
- `WorkspaceProfile`: id, name, role/team, canonical root path and created timestamp. The same Windows path can be registered only once.
- `LocalRepository`: id, workspace profile id, canonical path, directory name, present/missing status and last inventory time. This first inventory snapshot does not yet claim a GitHub repository identity; Phase 05 adds remote identity checks.
- `Workspace`: legacy v1 settings table retained for migration compatibility.
- `Repository`: immutable GitHub id, full name, canonical HTTPS URL, default branch, visibility/status, description/topics snapshot, fetched timestamp/etag.
- `Worktree`: id, repository id, canonical path, kind (`Standalone`/`Submodule`), parent worktree/path, pinned SHA, current branch, dirty state snapshot, last inventory time. Multiple worktrees per repository are valid.
- `SetupSession`: id, plan hash, mode, root, status, timestamps, cancellation/resume metadata.
- `SetupItem`: session/repository id, target path, step/status, attempt, safe error code, bytes estimate/known progress, linked worktree id.
- `SetupPlan` / `SetupProgress` (schema v3): a versioned JSON plan and per-repository state/result, saved after each item so cancelled or interrupted setup can be reopened and resumed. Clone URLs are public HTTPS identifiers; no token or private credential is stored.
- `ApiCache`: resource key, etag, expiry, fetched timestamp and serialized non-secret data.
- `OperationHistory`: operation id/type, target ids, outcome and redacted diagnostics.

## Invariants
- A repository ID may map to many worktrees; do not deduplicate distinct standalone/submodule worktrees.
- Canonical path is unique locally. Before use, compare expected remote identity and inspect `.git`/gitdir.
- Phase 04 inventory scans only direct child directories, skips reparse points and only checks for a `.git` marker; it never runs Git or reads repository config.
- No authentication token, private key, secret, LFS object or source content is stored in cache.
- Database deletion is never required for logout; provide explicit cache clearing with an impact preview.
