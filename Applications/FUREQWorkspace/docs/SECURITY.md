# Security

## Threat assumptions
All organization repositories and Projects are Public. Repository Markdown, Git config, hooks, filters, submodules, manifests, issue content, links and downloaded tools are untrusted. Public does not grant permission to modify and does not imply an open-source license.

## Authentication
- Guest mode for public discovery/clone/read.
- Proposed GitHub App user Device Flow for API actions. Tokens are short-lived; use refresh token only when enabled and store access/refresh credentials in Windows Credential Manager, with DPAPI protection as fallback.
- Git HTTPS credential is managed separately by Git Credential Manager. Detect username/account mismatch before a write.
- App installation permissions are the intersection of app grant and signed-in user's GitHub permissions. No PAT prompts, copied CLI token, private key or client secret in the app.

## Least privilege
Proposed app grants: repository metadata/content, issues and pull requests read/write; organization members read; organization projects read/write. No repository administration, Actions, billing, member write, permission changes or owner operations. Install only to the initial 14 repos. Validate these permission names in the registration UI before installation.

## Git and files
Use ArgumentList, no shell interpolation, HTTPS URL allowlist for `github.com/FUREQ-DEVELOP`, path canonicalization and reparse-point checks. Never run hooks, arbitrary scripts, downloaded tools or project configuration automatically. Do not overwrite non-empty/unrecognized folders. Refuse destructive sync when dirty, conflicted, detached or remote is unexpected.

## Logs/database
No tokens, authorization headers, credential output, complete private paths unless needed, or unnecessary source/issue content in logs. Local DB holds only settings, worktree inventory, cache and resumable setup state. Define bounded retention before Phase 02. Do not commit live DBs.

## Public-content rule
Do not write confidential or personal information, licensed assets without redistribution rights, or credentials. Do not modify repository visibility, team access, branch protections or existing GitHub configuration from the app. Secret leakage response and history rewrite require owner action.
