# 要件

## 目的
FUREQ-DEVELOPの全開発班が使う日本語Windowsデスクトップアプリ。GitHub Desktop/WebやVisual Studioを置き換えず、Organizationの開発環境を安全にセットアップし、GitHub/Git操作へつなぐ。

## 対象
- Windows 10/11 x64。リリース時にサポート対象Windowsで実機確認する。
- WPF, C#, MVVM。承認済み基準は.NET 10 LTSとVisual Studio 2026。
- 対象Organizationは `FUREQ-DEVELOP`。初期Full Setupは指定の14 repo。新規repo、他Organization、製品実装は対象外。
- 現行14 repo以外（例: `Site`）は自動追加せず、利用者が選べる追加候補として表示する。

## MVP機能（PHASE 02-09）
1. ゲスト表示、GitHub Appユーザー認証、Organization/Team/repository検出。
2. Team別・職種別WorkspaceとローカルRepository Inventory。
3. Repositoryのclone/fetch、安全なpull、branch/status/diff/commit/push/PRへの導線。
4. Submodule/Nested Submodule状態、再帰取得、固定SHAと更新候補の表示。
5. Full/Rendering/Engine/Game/Shared/Role/Custom Setup、計画、進捗、中断/再開、検証。
6. LFS状態と取得方式、Issue/Project/PRの基本表示と操作。
7. 開発環境診断、共通マニュアル/ツールへの入口、エラーと操作ログ。
8. Windows x64 self-contained ZIPの試験配布。

## 後続機能
PHASE 10-13でMarkdown文書管理、アセット閲覧/提出支援、Manifest経由の共通ツール連携、依存関係グラフと班間要求を追加する。3D preview、Actions管理、更新自動化は別途優先度を決める。

## 必須安全要件
- GitHub権限の迂回不可。public読み取りと認証済み書き込みを分ける。
- 変更前に対象/影響を示し、未コミット変更や不明な状態で破壊的操作をしない。
- API、Git、LFSの失敗を区別し、一部成功したデータを保全する。
- トークン、個人情報、非公開情報をリポジトリやログへ保存しない。
- 無料枠超過、課金、追加データ送信を前提としない。

## MVP受け入れ条件
- ゲストで14 repo情報を取得し、選択済みの環境を保存先に作れる。
- 再帰Submodule取得、dirty/既存repo保護、中断再開、キャンセルが安全に動く。
- 認証済みユーザーは自分の権限内でIssue/PR等を操作できる。
- 初心者向けUI、アクセシビリティ、エラー案内がある。
- 必須Unit/Integration試験成功、Windows x64ビルド・クリーン環境試験完了。
- 新規repoなし、mainへの直接pushなし、リリースは個別承認後。
