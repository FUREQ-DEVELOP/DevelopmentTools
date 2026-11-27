# 開発計画

| Phase | 内容 | 完了条件 |
|---|---|---|
| 00 | Existing environment read-only research | 差分/制約/承認事項報告 |
| 01 | Requirements, architecture, UI/API/security/data/setup/tool/test docs, AGENTS | 設計PR reviewed |
| 02 | WPF host, navigation, logging/settings/SQLite shell | clean build, startup, migration tests |
| 03 | Guest + GitHub App auth, org/team/repo discovery | permission/token/cancel/rate-limit tests |
| 04 | Workspace profiles, local inventory | duplicate identity/path tests |
| 05 | Git runner/status/branch/commit/fetch/pull/push/PR | safety tests and local-repo integration |
| 06 | Submodule graph, recursive init, pinned SHA/update preview | nested local graph tests |
| 07 | Full Workspace Setup, plan/progress/resume/LFS modes | all 14 public repos clean-clone test, then team pilot |
| 08 | Issues, Projects and PR basics | read/write via user permissions, no production test writes |
| 09 | MVP integration, Windows x64 self-contained ZIP trial | required tests and clean Windows trial |
| 10 | Markdown document browsing/editing/search/templates | safe rendering and Git diff tests |
| 11 | Asset browser/requests/LFS status | thumbnails do not download unselected payloads |
| 12 | Tool Manifest and controlled launch | malicious path/manifest tests |
| 13 | Dependency graph, SDK/API changes and cross-team requests | graph from gitlinks plus declared edges |
| 14-17 | Actions/Release/notifications, app updates, hardening, formal ZIP release | separate approval for external release |

Keep each phase in its own PR-sized change. Do not start product engine/rendering implementation.
