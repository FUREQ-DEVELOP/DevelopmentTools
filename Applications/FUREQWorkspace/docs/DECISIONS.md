# 設計判断

## D-001: .NETとIDE
- 状態: 承認済み
- 決定: .NET 10 LTS / WPF / Visual Studio 2026を基準にする。
- 理由: 現在の.NET 10 LTSは2028-11-14までサポート。VS2022の対応.NET 9は2026-11-10にサポート終了。
- 影響: チーム開発端末はVS2026へ移行が必要。ローカルはVS2026 18.7.3 / SDK 10.0.301のため、Visual Studio Installerと.NET SDKをサポート中の servicing bandへ更新する（実行未了）。

## D-002: 認証
- 状態: 方針承認済み、App登録/Org install未実施
- 決定: GitHub AppのユーザーDevice Flowを優先。App用Client ID以外の秘密鍵/Client Secretはdesktopへ含めない。APIとGit Credential Managerは別credentialとして扱い、アカウント不一致を検出する。
- 最小権限案: repo metadata/content read/write、issues read/write、pull requests read/write、organization members read、organization projects read/write。インストール対象は初期14 repo。実際の設定時にGitHubの権限画面とAPI操作を照合する。
- 必要操作: Organization ownerがApp登録・Device Flow有効化・14 repoへのinstallを承認。

## D-003: Application architecture
- 状態: 承認済み
- 決定: WPF MVVMのmodular monolith。App/Domain/Application/Infrastructureの最小分割。dynamic plugin systemなし。

## D-004: Repository/Release
- 状態: 承認済み
- 決定: 新規repoなし。実装はDevelopmentTools内のfeature branch/PR。試験配布はwin-x64 self-contained ZIP、正式Releaseは別途承認。

## 保留
- Visual Studio 2026/SDK updateの実行完了日は未確認。
- GitHub App Client ID、install ID、app permission実値。
- app signing/installer（MVPに含めない）。
- OS最小buildの公式サポート基準、アセットcache上限。
