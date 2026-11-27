# Common tool integration

PHASE 12 integrates existing DevelopmentTools tools. Phase 00 found none in `main`; do not show AssetValidator/DependencyChecker/etc. as available until a real executable/manifest exists.

## Manifest contract (Phase 12)
Place `*.fureq-tool.json` under `Tools/`. Version 1 fields are `schemaVersion`, `toolId`, `displayName`, `description`, `version`, `entryPoint`, `executionType`, `sideEffectClassification`, `timeoutSeconds`, and `arguments`. Only `executionType: executable` with an existing `.exe` inside this checkout is supported. Unknown fields, invalid JSON, unsupported versions/types, excessive values, and paths through reparse points are rejected. Manifests are bounded to 64 KiB; discovery is bounded to 5,000 directories and 1,000 valid tools.

## Execution safety
- Only launch registered tools inside an approved DevelopmentTools checkout; no separate installation path is supported.
- Do not execute arbitrary command text from GitHub, a Submodule, issue, README, or manifest automatically.
- Preview source/version, args, working directory, target and side effects; require confirmation for writes/network/install.
- Use ArgumentList, timeout and cancellation. Standard output/error are drained and discarded so tools cannot echo secrets into app logs/UI. Child processes receive only system path/temp variables, not the app's full environment. Persist result status but not secrets.
- Version 1 has no available manifests in the current checkout, so the tool list remains empty and no tool can be launched until a reviewed tool and manifest are added.
- Standalone tools must remain usable without FUREQ Workspace. The app is an entry point, not a runtime dependency of other tools.
