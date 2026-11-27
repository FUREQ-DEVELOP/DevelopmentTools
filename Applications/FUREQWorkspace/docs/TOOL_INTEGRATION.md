# Common tool integration

PHASE 12 integrates existing DevelopmentTools tools. Phase 00 found none in `main`; do not show AssetValidator/DependencyChecker/etc. as available until a real executable/manifest exists.

## Manifest contract (future)
Fields: SchemaVersion, ToolId, DisplayName, Description, Version, EntryPoint, ExecutionType, SupportedPlatforms, SupportedWorkspaces, SupportedRoles, RequiredTools, InputSchema, OutputSchema, RequiredPermissions, Timeout, SideEffectClassification. Validate schema/version/path/inputs before launch.

## Execution safety
- Only launch registered tools inside an approved DevelopmentTools checkout, or a specifically approved installation path.
- Do not execute arbitrary command text from GitHub, a Submodule, issue, README, or manifest automatically.
- Preview source/version, args, working directory, target and side effects; require confirmation for writes/network/install.
- Use ArgumentList, timeout, cancellation and redacted output. Persist result status but not secrets.
- Standalone tools must remain usable without FUREQ Workspace. The app is an entry point, not a runtime dependency of other tools.
