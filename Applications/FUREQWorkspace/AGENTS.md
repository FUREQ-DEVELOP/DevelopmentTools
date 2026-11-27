# FUREQ Workspace contributor instructions

## Scope
- This folder contains the Windows desktop app only. Shared utilities belong in the DevelopmentTools root only when more than one tool needs them.
- Do not modify other repositories, create repositories, change GitHub permissions, publish releases, or add paid services.
- Keep the app usable without authentication for public repository browsing and cloning. Never bypass a user's GitHub permissions.
- This is a public repository: do not add tokens, personal data, private contract material, unlicensed assets, or real user data.

## Architecture
- Target `net10.0-windows` / `win-x64`, WPF, C# and MVVM. Keep the initial app a modular monolith.
- Dependency direction: Presentation -> Application -> Domain; Infrastructure implements Application ports; App is the composition root. Domain must not depend on WPF, SQLite, Git, or GitHub.
- Use the smallest project and dependency set that meets the approved requirements. Do not add runtime plugin loading, a server, or a new repository.
- Git and Git LFS run as child processes through one safe process service. Use `ProcessStartInfo.ArgumentList`, explicit working directory, asynchronous output/error, cancellation, timeout and exit-code handling. Never concatenate shell commands or log credentials.

## Data and safety
- GitHub is the source of truth. SQLite is local state/cache only.
- Store tokens only in Windows Credential Manager / DPAPI protected storage, never in SQLite, JSON, environment dumps, URLs, command arguments or logs.
- Never discard, reset, stash, overwrite, auto-commit or force-push user changes. Show a plan before multi-repository changes.
- Treat repositories, submodules, hooks, Git filters, Markdown, manifests and downloaded tools as untrusted input. Validate URLs and paths.
- Do not install software, change environment variables, download bulk LFS data, or publish a release without user approval.

## Workflow
- Work on a feature branch and submit a Pull Request; do not push to `main`.
- Read `docs/DEVELOPMENT_STATUS.md` and the relevant design document before implementing. Update the status and decision log as work completes.
- Follow the tests in `docs/TEST_STRATEGY.md`. Report build, test, UI and real GitHub verification separately. Never claim a check that was not run.
