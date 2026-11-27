# 設計判断

## D-001: .NETとIDE
- 状態: 承認済み
- 決定: .NET 10 LTS / WPF / Visual Studio 2026を基準にする。
- 理由: 現在の.NET 10 LTSは2028-11-14までサポート。VS2022の対応.NET 9は2026-11-10にサポート終了。
- 影響: チーム開発端末はVS2026へ移行が必要。ローカルはVS2026 18.7.3 / SDK 10.0.301のため、Visual Studio Installerと.NET SDKをサポート中の servicing bandへ更新する（実行未了）。

## D-002: 認証
- 状態: GitHub App登録・初期14 repository install済み。実機Device Flow/API接続は未検証
- 決定: GitHub AppのユーザーDevice Flowを優先。App用Client ID以外の秘密鍵/Client Secretはdesktopへ含めない。APIとGit Credential Managerは別credentialとして扱い、アカウント不一致を検出する。
- 最小権限案: repo metadata/content read/write、issues read/write、pull requests read/write、organization members read、organization projects read/write。インストール対象は初期14 repo。実際の設定時にGitHubの権限画面とAPI操作を照合する。
- 実設定: `FUREQ Workspace`、Device Flow有効、Webhook無効、repository metadata read (mandatory)/contents read-write/issues read-write/pull requests read-write、organization members read/projects read-write。初期14 repositoryを選択し、Siteは選択しない。
- GitHubはAppに秘密鍵を1つ以上要求し、最後の鍵を削除できない。1鍵がApp側に残る。秘密鍵やClient Secretをデスクトップアプリへ含めない。

## D-003: Application architecture
- 状態: 承認済み
- 決定: WPF MVVMのmodular monolith。App/Domain/Application/Infrastructureの最小分割。dynamic plugin systemなし。

## D-004: Repository/Release
- 状態: 承認済み
- 決定: 新規repoなし。実装はDevelopmentTools内のfeature branch/PR。試験配布はwin-x64 self-contained ZIP、正式Releaseは別途承認。

## D-005: Git writes in the desktop app
- 状態: PHASE 05実装済み、PRレビュー待ち
- 決定: Git操作は単一のProcessRunnerを通し、hooksを無効化する。PushはGitHub AppユーザーとGit Credential Managerアカウントの一致確認後、保護ブランチ以外にのみ許可する。Pullはcleanなworktreeに対するorigin追跡先からのfast-forwardのみ。GitHubへの書き込みはUIで毎回確認する。
- 理由: public repositoryの設定、hooks、credential helpersを信頼せず、意図しない・取り消しにくいGitHub書き込みを抑止する。
- 検証: fake HTTPと一時ローカルbare repositoryで検証。実OrganizationへのPush/PR作成は未実施。

## D-006: Submodule initialization
- 状態: PHASE 06実装済み、PRレビュー待ち
- 決定: `.gitmodules`とHEADのgitlinkから依存を走査し、URL/パスを検証した後、ユーザー確認を経て1階層ずつ初期化する。記録されたSHAを維持し、追跡ブランチの最新へ自動更新しない。
- 安全策: hooks/LFS smudgeを無効化し、Gitのglobal/system configとテンプレートを隔離する。初期化済みモジュールのSHA差分はプレビューだけ行う。
- 検証: nested local repositoriesによる再帰初期化とSHA差分プレビュー。組織リポジトリへの実行は未実施。

## D-007: Workspace Setup execution
- 状態: PHASE 07 implementation PR作成予定。Live 14-repository pilot未実施。
- 決定: versioned JSONから相対パスを解決し、モード別dependency closureとSetup Planを表示する。明示確認後、最大3件を並行にstaging cloneし、origin/branch/SHAを検証してtargetへ昇格する。完了済みと各itemの状態をSQLiteへ保存し再開可能にする。
- LFS: Development modeだけ実行可能。Lightweight/Complete/Customは見積もり/対象選択の機能が揃うまでBlockする。
- 検証: ローカルbare reposでclone/cancel/resumeを検証。全14個の組織公開repoへの実行はしない。

## D-008: Issue/PR/Projects integration
- 状態: PHASE 08実装済み、live API検証待ち
- 決定: Issues/PRsはREST、Organization ProjectsはProjects v2 GraphQLで読む。Issue作成とProjectへの追加は別々に確認する。認証ユーザーのApp tokenとGitHub側の権限を尊重し、タイムアウトした書き込みは自動再試行しない。
- 検証: fake HTTPで読み書きpayloadとtoken headerを確認。実Organizationへの書き込みは未実施。

## D-009: Markdown safety
- 状態: PHASE 10実装済み、PRレビュー待ち
- 決定: Markdownは初期版ではHTMLとして描画せず、ソーステキストとして閲覧・編集する。検索は選択中のローカルGit repo内の`.md`だけ。保存前に相対パスを表示して確認する。
- 制限: リポジトリ外/リンク先を拒否し、ファイル2 MiB、走査50 MiB、件数5,000件を上限とする。テンプレートはアプリ内固定文面のみ。
- 検証: path validation、検索/保存、Git diffの一時リポジトリ試験。

## D-010: Asset inventory and LFS
- 状態: PHASE 11実装済み、PR作成/レビュー待ち
- 決定: 資産一覧は選択中のローカルGit repositoryから画像ファイルとGit LFS pointerだけを列挙し、Issue依頼は既存の認証済みAPIを使って毎回確認する。
- 安全策: `.git`等の除外、reparse pointを辿らない、走査2 GiB/20,000件の上限を設ける。LFS pointerのOID/sizeを読み取るだけで画像やLFS objectをダウンロードしない。Issueに未登録ラベルを仮定しない。
- 検証: 一時Git repositoryで画像、依存フォルダ除外、CRLF pointer解析、pointer不変を検証。

## 保留
- Visual Studio 2026/SDK updateの実行完了日は未確認。
- GitHub App Client ID、install ID、app permission実値。
- app signing/installer（MVPに含めない）。
- OS最小buildの公式サポート基準、アセットcache上限。
