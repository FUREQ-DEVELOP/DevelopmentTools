# Security

## Threat assumptions
All organization repositories and Projects are Public. Repository Markdown, Git config, hooks, filters, submodules, manifests, issue content, links and downloaded tools are untrusted. Public does not grant permission to modify and does not imply an open-source license.

## Authentication
- Guest mode for public discovery/clone/read.
- GitHub App user Device Flow for API actions. Request `offline_access` so GitHub returns expiring tokens and a refresh token; store the pair in Windows Credential Manager. Never put them in SQLite, JSON files, environment variables, command arguments or logs.
- Device verification URLs are accepted only when they are HTTPS `github.com/login/device`. The public Client ID identifies the registered App and is not a secret; the App private key is never shipped in the desktop app.
- Authenticated repository results are constrained by the intersection of the user's access and the GitHub App's installed permissions/repository selection. Team results are filtered to FUREQ-DEVELOP.
- Git HTTPS credential is managed separately by Git Credential Manager. Detect username/account mismatch before a write.
- Network Git commands explicitly clear configured credential helpers and invoke Git Credential Manager. The app reads only the returned username for account comparison; it never logs or stores the returned credential.
- App installation permissions are the intersection of app grant and signed-in user's GitHub permissions. No PAT prompts, copied CLI token, private key or client secret in the app.

## Least privilege
Proposed app grants: repository metadata/content, issues and pull requests read/write; organization members read; organization projects read/write. No repository administration, Actions, billing, member write, permission changes or owner operations. Install only to the initial 14 repos. Validate these permission names in the registration UI before installation.

## Git and files
Use ArgumentList, no shell interpolation, HTTPS URL allowlist for `github.com/FUREQ-DEVELOP`, path canonicalization and reparse-point checks. Never run hooks, arbitrary scripts, downloaded tools or project configuration automatically. Disable hooks for Git operations. Do not overwrite non-empty/unrecognized folders. Refuse pull when dirty, conflicted, detached, without an `origin/*` upstream, or when the remote is unexpected. Pull is fast-forward-only; push refuses `main`/`master`, force-push is unavailable, and commit only uses user-staged changes. Confirm each write in the UI.

## Logs/database
No tokens, authorization headers, credential output, complete private paths unless needed, or unnecessary source/issue content in logs. Local DB holds only settings, worktree inventory, cache and resumable setup state. Define bounded retention before Phase 02. Do not commit live DBs.

## Public-content rule
Do not write confidential or personal information, licensed assets without redistribution rights, or credentials. Do not modify repository visibility, team access, branch protections or existing GitHub configuration from the app. Secret leakage response and history rewrite require owner action.
