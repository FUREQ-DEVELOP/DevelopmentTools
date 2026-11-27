# アーキテクチャ

## 技術
WPF / C# / `.NET 10 LTS` (`net10.0-windows`, x64), MVVM, Generic Host/DI, SQLite, `HttpClient` + `System.Text.Json`, Git/Git LFS CLI。Windows 11のVisual Studio 2026を基準にする。Visual Studio 2022は.NET 10 targetの公式構成ではない。

候補package: CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.15, Microsoft.Data.Sqlite 10.0.15。すべてCPMで集中管理し、実装開始時に脆弱性/ライセンス/最新patchを再確認する。OctokitはREST/GraphQL要件を満たすか確かめるまで入れず、標準`HttpClient`を使う。Markdig/AvalonEditはPHASE 10まで保留。

## ソリューション構成
- `FUREQ.Workspace.App`: WPF entry point / composition root。
- `FUREQ.Workspace.Domain`: repository/workspace/setup value typesと不変条件。
- `FUREQ.Workspace.Application`: use cases、interfaces、Setup Plan、進捗/結果。
- `FUREQ.Workspace.Infrastructure`: GitHub API、Git process、file system、Credential Manager/DPAPI、SQLite。
- `Presentation`は初期はApp内に置き、必要になった時だけ分離。dynamic DLL pluginsは作らない。
- Unit testsとIntegration testsを別プロジェクトにする。UI automation projectは実機要件が固まってから追加。

## 依存方向
`Presentation -> Application -> Domain`。InfrastructureはApplicationのportを実装し、Appで合成する。DomainからWPF/SQLite/GitHub SDKを参照しない。

## 主要機能境界
Authentication, OrganizationDiscovery, Workspaces, RepositoryInventory, Setup, Git, Submodules, Lfs, Issues/Projects/PullRequests, Environment, Documents, Assets, Tools, Dependencies, Settings。MVPではSetup/GitHub/Git/Submodule/Inventoryを先行し、残りは導線または後続phaseにする。

## データとプロセス
GitHubが共有データの正本。SQLiteはユーザー設定、ローカルworktree、setup resume、cacheのみ。外部処理はGit CLIかHTTPS API。アプリ内から任意シェル文字列を実行しない。

## 配布
`win-x64` self-contained publishをZIP化し、DevelopmentToolsのGitHub Releaseで配布する。インストーラー、署名証明書、別サービスはMVP必須にしない。公開前にrelease approvalを得る。
