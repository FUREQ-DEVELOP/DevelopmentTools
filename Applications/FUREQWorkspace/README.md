# FUREQ Workspace

FUREQ-DEVELOP向けのWindows共同開発ポータルです。GitHub、Git、Submodule、開発環境セットアップへの入口を提供します。

開発は [要件](docs/REQUIREMENTS.md)、[設計](docs/ARCHITECTURE.md)、[進捗](docs/DEVELOPMENT_STATUS.md)から始めます。

現在はPHASE 02の初期実装です。WPFホスト、画面ナビゲーション、SQLiteスキーマv1を実装しています。GitHub認証、Organization API、リポジトリ操作は未実装です。

## 開発

- Visual Studio 2026、.NET SDK 10.0.404を使用します（`global.json`で固定）。
- `dotnet build FUREQ.Workspace.sln --configuration Release`
- `dotnet test FUREQ.Workspace.sln --configuration Release`
- WPFアプリ: `src/FUREQ.Workspace.App/FUREQ.Workspace.App.csproj`
- ローカルDB: `%LOCALAPPDATA%\FUREQ\FUREQWorkspace\workspace.db`

DBには端末設定のみを保存します。GitHubトークンや秘密情報を保存しません。
