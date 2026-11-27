# UI設計

## 全体
- 左Navigation: Dashboard, Workspaces, Setup, Repositories, Tasks, Documents, Assets, Tools, Dependencies, Settings。MVPで未実装の項目は隠すか「準備中」と表示し、動作するように装わない。
- 上部: Workspace selector、Org/account、sync状態。下部Status bar: 接続、現在の処理、通知。
- 日本語を初期言語とする。ダーク/ライトとOS high-contrast、キーボード操作、DPI scalingを考慮。

## 初回Setup wizard
1. GuestまたはSign in
2. Setup mode
3. 保存先
4. Git/Git LFSとディスクの診断
5. 対象repo選択
6. Submoduleと既存worktreeの確認
7. LFS方式と通信量/容量の警告
8. Setup Planの最終確認
9. 進捗・詳細ログ
10. 完了/部分成功レポート

## Setup plan/進捗
Planにはrepo名/ID、保存先、branch、既存repo判定、submodule、LFS、必要ツール、warning/errorを表示。Progressでは各repoの状態・処理・submodule/LFS・経過時間・retry可否を表示し、全体進捗を不明な時はindeterminateとする。成功済みを失敗時に消さない。Cancelは安全な境界で停止し、再開情報を保存。

## Git操作
Dirty状態・branch・commitをカードで見せる。Pull/同期の前に変更と影響を示し、未コミット変更を検出したら安全な選択肢を表示する。Danger actionは対象repositoryと具体的影響を確認dialogで提示。

## Accessibility
最低contrast、Tab順、focus表示、screen reader label、keyboard-onlyの主要操作、文字拡大を検証する。色だけで状態を示さず、文字/アイコンを併用。
