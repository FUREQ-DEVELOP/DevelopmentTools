# API contracts

## GitHub
- Public guest requests use HTTPS GitHub REST/GraphQL without auth where allowed. Authenticated requests use the approved GitHub App user token; never substitute another account or admin credential.
- Repository discovery is paginated and keyed by immutable repository ID; collect name/full name, description, visibility, clone/default branch, archived/fork/disabled, updated time, topics and permissions where available. Treat missing/inaccessible fields distinctly.
- Teams and membership require authentication and are displayed only for the signed-in user. GitHub permissions remain authoritative.
- Issues, Projects and Pull Requests use typed request/response models and explicit read/write permissions. No Organization administration endpoints in MVP.
- REST calls send `X-GitHub-Api-Version: 2026-03-10` and `Accept: application/vnd.github+json`; reconfirm the supported version when implementing future endpoints. GraphQL uses its current schema. Add ETags/conditional requests and rate-limit/Retry-After handling as the read surfaces expand.
- PHASE 08 lists issues and pull requests via the REST endpoints; filter pull requests out of the REST issue list. Issue creation is an authenticated write. Organization Projects v2 and items use GraphQL; adding an Issue to a Project is a separately confirmed mutation.

## Application result
Every async use case accepts CancellationToken and returns a typed `Result<T>` with `Success`, `Failure`, or `OutcomeUnknown`; include stable error code, safe user message, retryability and redacted diagnostic correlation ID. Do not retry a timed-out write unless idempotency/current remote state is checked.

## Pagination/cache
Paginate until next page is absent. Cache read-only metadata with ETag, fetched timestamp and expiry. Show stale/offline status. Never serve cached authorization as current permission.

## Git subprocess
One process runner uses ArgumentList, explicit cwd, async stdout/stderr, timeout, cancellation and exit code. Output parsers use porcelain/NUL formats where available. Command arguments and output are redacted before logging. Network operations use Git Credential Manager only. Git hooks are disabled for app-invoked operations. Pull is clean-tree, `origin/*`, fast-forward-only; push refuses protected branches and checks account identity first.

## Submodules
Build the dependency graph from committed gitlinks plus a tracked, clean `.gitmodules`. Show each path, FUREQ-DEVELOP repository, pinned SHA, current SHA and initialization state. Initialization proceeds one validated level at a time, uses the recorded SHA and never uses `--remote`. LFS smudge and hooks are disabled. Existing initialized submodules at a different SHA are reported in an update preview; they are not changed by initialization.

## Workspace setup
Versioned JSON maps the 14 default repositories to relative paths. Modes compute and display a dependency closure before execution. Revalidate plan identity/path/origin at execution, clone into staging, validate default branch and remote commit, initialize only pinned submodules, then promote to a vacant target. Persist the plan and each item's status locally for resume. Limit top-level clone concurrency to three. Development LFS mode skips object downloads; other modes require a future estimate and explicit path/budget selection implementation.

## File and path
Target paths derive from approved mapping plus validated overrides. Reject path traversal, root escape, reserved Windows names and reparse-point escapes. Verify remote canonical URL/repository ID before reusing a clone.
