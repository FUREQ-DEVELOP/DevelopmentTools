# Full Workspace Setup

## Default 14 repositories and paths
| Category | Repository | Target relative path |
|---|---|---|
| Organization | `.github` | `Organization/.github` |
| Organization | `.github-private` | `Organization/.github-private` |
| Shared | `DevelopmentDocs` | `Shared/DevelopmentDocs` |
| Shared | `DevelopmentTools` | `Shared/DevelopmentTools` |
| Rendering | `RenderingLibrary` | `Rendering/RenderingLibrary` |
| Rendering | `RenderingWorkspace` | `Rendering/RenderingWorkspace` |
| Engine | `GameEngine` | `Engine/GameEngine` |
| Engine | `EngineEditor` | `Engine/EngineEditor` |
| Engine | `EngineHub` | `Engine/EngineHub` |
| Engine | `EngineWorkspace` | `Engine/EngineWorkspace` |
| Game | `Game-001` | `Games/Game-001/Game-001` |
| Game | `Game-001-Code` | `Games/Game-001/Game-001-Code` |
| Game | `Game-001-Art` | `Games/Game-001/Game-001-Art` |
| Game | `Game-001-Design` | `Games/Game-001/Game-001-Design` |

Store mapping in versioned JSON schema, not source absolute paths. Allow flat layout and user overrides. `Site` is an additional candidate only.

## Modes
- Full: 14 defaults.
- Rendering: RenderingWorkspace, RenderingLibrary, DevelopmentTools, DevelopmentDocs.
- Engine: EngineWorkspace, GameEngine, EngineEditor, EngineHub, DevelopmentTools, DevelopmentDocs, RenderingLibrary.
- Game: Game-001, Game-001-Code, Game-001-Art, Game-001-Design, GameEngine, RenderingLibrary, DevelopmentTools, DevelopmentDocs.
- Shared Services: DevelopmentTools, DevelopmentDocs, `.github`, `.github-private`.
- Role-based: necessary shared repos plus role repository; dependency closure shown before run.
- Custom: explicit selection with dependencies shown and overrideable.

## Planning/execution
1. Read-only diagnostics, repository listing, disk check, path scan, remote/identity detection, `.gitmodules` dependency graph, tooling and estimated LFS.
2. Show Setup Plan with unique id, repo id/url/branch/path, existing state, dependencies, LFS mode/estimate, warnings/blockers and actions. Revalidate before execution.
3. Confirm, create no more than three top-level clones concurrently. Serialize worktree-specific operations. Initialize submodules recursively with bounded jobs and configured LFS mode.
4. Validate origin, branch/commit, gitlinks, nested modules, LFS status, and preserve all successful items. Save resumable state after every item.
5. Report success, partial success, skipped, blocked and failure with next action.

## Existing folders/repositories
- If exact repository id/url and valid worktree are found, offer Reuse; do not clone over it.
- If directory is non-empty or remote identity differs, block and ask user to pick another path or explicitly register an existing repository.
- Do not reset, clean, stash, checkout, pull or update nested gitlinks during discovery.
- Clone to an app-created staging directory and promote only after validation. Cancel leaves completed repos intact and records resumable items.

## LFS modes
- Development default: skip automatic smudge/download, show pointer count/state.
- Lightweight: selected repo/paths only after estimate.
- Complete: all LFS objects only after explicit confirmation of estimate/free budget; never assume unlimited use.
- Custom: per repository/path selection.
All remain configurable; never create large test assets.
