# FUREQ Workspace

FUREQ-DEVELOP向けのWindows共同開発ポータルです。GitHub、Git、Submodule、開発環境セットアップへの入口を提供します。

開発は [要件](docs/REQUIREMENTS.md)、[設計](docs/ARCHITECTURE.md)、[進捗](docs/DEVELOPMENT_STATUS.md)から始めます。

PHASE 03を実装中です。WPFホスト、SQLiteスキーマv1に加え、ゲストモードでOrganizationの公開リポジトリ一覧をページング取得して表示できます。GitHub Appによるユーザー認証、チーム情報、認証が必要なリポジトリ情報は未実装です。

## 開発

- Visual Studio 2026、.NET SDK 10.0.404を使用します（`global.json`で固定）。
- `dotnet build FUREQ.Workspace.sln --configuration Release`
- `dotnet test FUREQ.Workspace.sln --configuration Release`
- WPFアプリ: `src/FUREQ.Workspace.App/FUREQ.Workspace.App.csproj`
- ローカルDB: `%LOCALAPPDATA%\FUREQ\FUREQWorkspace\workspace.db`

DBには端末設定のみを保存します。GitHubトークンや秘密情報を保存しません。
