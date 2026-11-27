# DevelopmentTools contributor instructions

## Repository boundaries
- Keep shared utilities here only when multiple applications need them. Application-specific code belongs in its application folder.
- Do not create repositories or modify other repositories as part of a DevelopmentTools change.
- Preserve existing history, workflows, submodules, and user changes. Never push directly to `main`.
- Treat this public repository as public: do not commit tokens, personal data, private contract material, or unlicensed assets.

## Safe changes
- Read the target application's `AGENTS.md` and current `docs/DEVELOPMENT_STATUS.md` before implementation.
- Use a feature branch and pull request. Keep changes scoped and include relevant validation results.
- Do not install software, alter account permissions, fetch bulk LFS data, or publish a release without the user's approval.
- Do not claim builds, tests, or external state were verified unless they were actually checked.

## Shared tooling
- Prefer existing scripts and dependencies. Add a shared dependency only when at least two tools need it.
- Keep scripts explicit about their working directory and report failures with actionable messages.
- Do not overwrite existing GitHub Actions or shared configuration without checking current usage and compatibility.
