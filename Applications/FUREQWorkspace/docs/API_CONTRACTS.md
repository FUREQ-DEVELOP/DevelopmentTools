# API contracts

## GitHub
- Public guest requests use HTTPS GitHub REST/GraphQL without auth where allowed. Authenticated requests use the approved GitHub App user token; never substitute another account or admin credential.
- Repository discovery is paginated and keyed by immutable repository ID; collect name/full name, description, visibility, clone/default branch, archived/fork/disabled, updated time, topics and permissions where available. Treat missing/inaccessible fields distinctly.
- Teams and membership require authentication and are displayed only for the signed-in user. GitHub permissions remain authoritative.
- Issues, Projects and Pull Requests use typed request/response models and explicit read/write permissions. No Organization administration endpoints in MVP.
- REST calls send `X-GitHub-Api-Version: 2026-03-10` and `Accept: application/vnd.github+json`; reconfirm the supported version when implementing future endpoints. GraphQL uses its current schema. Add ETags/conditional requests and rate-limit/Retry-After handling as the read surfaces expand.

## Application result
Every async use case accepts CancellationToken and returns a typed `Result<T>` with `Success`, `Failure`, or `OutcomeUnknown`; include stable error code, safe user message, retryability and redacted diagnostic correlation ID. Do not retry a timed-out write unless idempotency/current remote state is checked.

## Pagination/cache
Paginate until next page is absent. Cache read-only metadata with ETag, fetched timestamp and expiry. Show stale/offline status. Never serve cached authorization as current permission.

## Git subprocess
One process runner uses ArgumentList, explicit cwd, async stdout/stderr, timeout, cancellation and exit code. Output parsers use porcelain/NUL formats where available. Command arguments and output are redacted before logging. Network operations use Git Credential Manager only. Git hooks are disabled for app-invoked operations. Pull is clean-tree, `origin/*`, fast-forward-only; push refuses protected branches and checks account identity first.

## File and path
Target paths derive from approved mapping plus validated overrides. Reject path traversal, root escape, reserved Windows names and reparse-point escapes. Verify remote canonical URL/repository ID before reusing a clone.
