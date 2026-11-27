using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Net.Http;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.App.Presentation;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly RelayCommand _navigateCommand;
    private readonly IRepositoryDiscovery _repositoryDiscovery;
    private readonly IGitHubAuthorization _authorization;
    private readonly IGitHubAccountDiscovery _accountDiscovery;
    private readonly IWorkspaceInventory _workspaceInventory;
    private readonly IGitRepositoryOperations _gitOperations;
    private readonly IGitHubPullRequestService _pullRequestService;
    private string _currentKey = "home";
    private string _repositoryStatus = "未取得";
    private string _authenticationStatus = "ゲストモード（公開情報のみ）";
    private CancellationTokenSource? _signInCancellation;
    private WorkspaceProfile? _selectedWorkspaceProfile;
    private string _workspaceName = "";
    private string _workspaceRole = "";
    private string _workspaceRootPath = "";
    private LocalRepositoryLocation? _selectedLocalRepository;
    private GitRepositorySnapshot? _gitSnapshot;
    private string _gitStatusText = "ローカルリポジトリを選択して状態を確認してください。";
    private string _newBranchName = "";
    private string _commitMessage = "";
    private string _pullRequestTitle = "";
    private string _pullRequestBody = "";
    private string _pullRequestBase = "main";

    public MainWindowViewModel(
        IRepositoryDiscovery repositoryDiscovery,
        IGitHubAuthorization authorization,
        IGitHubAccountDiscovery accountDiscovery,
        IWorkspaceInventory workspaceInventory,
        IGitRepositoryOperations gitOperations,
        IGitHubPullRequestService pullRequestService)
    {
        _repositoryDiscovery = repositoryDiscovery;
        _authorization = authorization;
        _accountDiscovery = accountDiscovery;
        _workspaceInventory = workspaceInventory;
        _gitOperations = gitOperations;
        _pullRequestService = pullRequestService;
        _navigateCommand = new RelayCommand(parameter => Navigate(parameter as string));
        LoadRepositoriesCommand = new AsyncRelayCommand(LoadRepositoriesAsync);
        SignInCommand = new AsyncRelayCommand(SignInAsync);
        SignOutCommand = new RelayCommand(_ => SignOut());
        CancelSignInCommand = new RelayCommand(_ => _signInCancellation?.Cancel());
        CreateWorkspaceProfileCommand = new AsyncRelayCommand(CreateWorkspaceProfileAsync);
        ScanLocalRepositoriesCommand = new AsyncRelayCommand(ScanLocalRepositoriesAsync);
        BrowseWorkspaceRootCommand = new RelayCommand(_ => BrowseWorkspaceRoot());
        GetGitStatusCommand = new AsyncRelayCommand(GetGitStatusAsync);
        CreateBranchCommand = new AsyncRelayCommand(CreateBranchAsync);
        FetchCommand = new AsyncRelayCommand(FetchAsync);
        PullFastForwardCommand = new AsyncRelayCommand(PullFastForwardAsync);
        CommitStagedChangesCommand = new AsyncRelayCommand(CommitStagedChangesAsync);
        PushCommand = new AsyncRelayCommand(PushAsync);
        CreatePullRequestCommand = new AsyncRelayCommand(CreatePullRequestAsync);
        Repositories = [];
        Teams = [];
        WorkspaceProfiles = [];
        LocalRepositories = [];
        GitChangedPaths = [];
        NavigationItems =
        [
            new NavigationItem("home", "ホーム", _navigateCommand),
            new NavigationItem("repositories", "リポジトリ", _navigateCommand),
            new NavigationItem("setup", "ワークスペース", _navigateCommand),
            new NavigationItem("settings", "設定", _navigateCommand)
        ];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public ObservableCollection<RepositoryMetadata> Repositories { get; }
    public ObservableCollection<GitHubTeam> Teams { get; }
    public ObservableCollection<WorkspaceProfile> WorkspaceProfiles { get; }
    public ObservableCollection<LocalRepositoryLocation> LocalRepositories { get; }
    public ObservableCollection<string> GitChangedPaths { get; }
    public ICommand LoadRepositoriesCommand { get; }
    public ICommand SignInCommand { get; }
    public ICommand SignOutCommand { get; }
    public ICommand CancelSignInCommand { get; }
    public ICommand CreateWorkspaceProfileCommand { get; }
    public ICommand ScanLocalRepositoriesCommand { get; }
    public ICommand BrowseWorkspaceRootCommand { get; }
    public ICommand GetGitStatusCommand { get; }
    public ICommand CreateBranchCommand { get; }
    public ICommand FetchCommand { get; }
    public ICommand PullFastForwardCommand { get; }
    public ICommand CommitStagedChangesCommand { get; }
    public ICommand PushCommand { get; }
    public ICommand CreatePullRequestCommand { get; }
    public string WorkspaceName { get => _workspaceName; set { _workspaceName = value; OnPropertyChanged(); } }
    public string WorkspaceRole { get => _workspaceRole; set { _workspaceRole = value; OnPropertyChanged(); } }
    public string WorkspaceRootPath { get => _workspaceRootPath; set { _workspaceRootPath = value; OnPropertyChanged(); } }
    public LocalRepositoryLocation? SelectedLocalRepository { get => _selectedLocalRepository; set { _selectedLocalRepository = value; OnPropertyChanged(); } }
    public GitRepositorySnapshot? GitSnapshot { get => _gitSnapshot; private set { _gitSnapshot = value; OnPropertyChanged(); } }
    public string GitStatusText { get => _gitStatusText; private set { _gitStatusText = value; OnPropertyChanged(); } }
    public string NewBranchName { get => _newBranchName; set { _newBranchName = value; OnPropertyChanged(); } }
    public string CommitMessage { get => _commitMessage; set { _commitMessage = value; OnPropertyChanged(); } }
    public string PullRequestTitle { get => _pullRequestTitle; set { _pullRequestTitle = value; OnPropertyChanged(); } }
    public string PullRequestBody { get => _pullRequestBody; set { _pullRequestBody = value; OnPropertyChanged(); } }
    public string PullRequestBase { get => _pullRequestBase; set { _pullRequestBase = value; OnPropertyChanged(); } }
    public WorkspaceProfile? SelectedWorkspaceProfile
    {
        get => _selectedWorkspaceProfile;
        set
        {
            _selectedWorkspaceProfile = value;
            OnPropertyChanged();
            _ = RefreshLocalRepositoriesSafelyAsync();
        }
    }
    public string CurrentKey => _currentKey;
    public string RepositoryStatus { get => _repositoryStatus; private set { _repositoryStatus = value; OnPropertyChanged(); } }
    public string AuthenticationStatus { get => _authenticationStatus; private set { _authenticationStatus = value; OnPropertyChanged(); } }

    public async Task InitializeAsync()
    {
        WorkspaceProfiles.Clear();
        foreach (var profile in await _workspaceInventory.GetProfilesAsync()) WorkspaceProfiles.Add(profile);
        _selectedWorkspaceProfile = WorkspaceProfiles.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedWorkspaceProfile));
        await LoadLocalRepositoriesAsync();
    }

    public string DatabaseStatus => "ローカル設定データベースを初期化しました。トークンはWindows資格情報マネージャーに保存します。";

    public string CurrentTitle => _currentKey switch
    {
        "repositories" => "リポジトリ",
        "setup" => "ワークスペース",
        "settings" => "設定",
        _ => "開発ワークスペース"
    };

    public string CurrentDescription => _currentKey switch
    {
        "repositories" => "FUREQ-DEVELOPのリポジトリを一覧し、ローカルの作業場所と状態を確認します。",
        "setup" => "班や職種ごとの作業プロファイルを管理し、指定フォルダー内のローカルリポジトリを確認します。",
        "settings" => "保存先、表示、同期方法など、この端末の設定を管理します。",
        _ => "開発環境の構築、共同開発、制作支援への入口です。"
    };

    private void Navigate(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key == _currentKey)
        {
            return;
        }

        _currentKey = key;
        OnPropertyChanged(nameof(CurrentKey));
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(CurrentDescription));
    }

    private async Task LoadRepositoriesAsync()
    {
        RepositoryStatus = "GitHubから公開リポジトリを取得中…";
        try
        {
            var accessToken = await _authorization.GetAccessTokenAsync();
            if (accessToken is null)
            {
                AuthenticationStatus = "ゲストモード（公開情報のみ）";
                Teams.Clear();
            }
            else
            {
                var account = await _accountDiscovery.GetCurrentAccountAsync(accessToken);
                var teams = await _accountDiscovery.GetTeamsAsync(accessToken);
                Teams.Clear();
                foreach (var team in teams) Teams.Add(team);
                AuthenticationStatus = $"{account.Name} (@{account.Login}) · チーム {teams.Count} 件";
            }

            var repositories = await _repositoryDiscovery.GetRepositoriesAsync(accessToken);
            Repositories.Clear();
            foreach (var repository in repositories)
            {
                Repositories.Add(repository);
            }

            RepositoryStatus = $"{Repositories.Count} 件のリポジトリを取得しました。";
        }
        catch (HttpRequestException)
        {
            RepositoryStatus = "GitHub APIに接続できませんでした。ネットワークを確認して再試行してください。";
        }
        catch (TaskCanceledException)
        {
            RepositoryStatus = "取得がタイムアウトしました。再試行してください。";
        }
        catch (Exception)
        {
            RepositoryStatus = "リポジトリを取得できませんでした。時間をおいて再試行してください。";
        }
    }

    private async Task SignInAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _signInCancellation = cancellation;
        try
        {
            var request = await _authorization.BeginAsync(cancellation.Token);
            Process.Start(new ProcessStartInfo(request.VerificationUri.AbsoluteUri) { UseShellExecute = true });
            var result = MessageBox.Show(
                $"ブラウザーでGitHubにサインインし、このアプリを承認してください。\n\nURL: {request.VerificationUri}\nコード: {request.UserCode}\n\n承認後にOKを押すと、完了を確認します。",
                "GitHubでサインイン",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            if (result != MessageBoxResult.OK) return;

            RepositoryStatus = "GitHubの承認を確認中…";
            await _authorization.CompleteAsync(request, cancellation.Token);
            var accessToken = await _authorization.GetAccessTokenAsync(cancellation.Token);
            if (accessToken is null) throw new InvalidOperationException("GitHubの認証情報を取得できませんでした。");
            var account = await _accountDiscovery.GetCurrentAccountAsync(accessToken, cancellation.Token);
            AuthenticationStatus = $"{account.Name} (@{account.Login})";
            await LoadRepositoriesAsync();
        }
        catch (OperationCanceledException)
        {
            RepositoryStatus = "GitHub認証をキャンセルしました。";
        }
        catch (Exception)
        {
            RepositoryStatus = "GitHub認証に失敗しました。接続を確認して、もう一度お試しください。";
        }
        finally
        {
            _signInCancellation = null;
        }
    }

    private void SignOut()
    {
        try
        {
            _authorization.SignOut();
            Teams.Clear();
            Repositories.Clear();
            AuthenticationStatus = "ゲストモード（公開情報のみ）";
            RepositoryStatus = "この端末からサインアウトしました。";
        }
        catch (Exception)
        {
            RepositoryStatus = "認証情報を削除できませんでした。Windows資格情報マネージャーを確認してください。";
        }
    }

    private async Task CreateWorkspaceProfileAsync()
    {
        try
        {
            var profile = await _workspaceInventory.CreateProfileAsync(WorkspaceName, WorkspaceRole, WorkspaceRootPath);
            WorkspaceProfiles.Add(profile);
            SelectedWorkspaceProfile = profile;
            RepositoryStatus = $"「{profile.Name}」プロファイルを保存しました。フォルダーは変更していません。";
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or InvalidOperationException or IOException)
        {
            RepositoryStatus = exception.Message;
        }
    }

    private async Task ScanLocalRepositoriesAsync()
    {
        var profile = SelectedWorkspaceProfile;
        if (profile is null)
        {
            RepositoryStatus = "先にワークスペースプロファイルを選択してください。";
            return;
        }

        try
        {
            var count = await _workspaceInventory.ScanRepositoriesAsync(profile.Id);
            if (SelectedWorkspaceProfile?.Id == profile.Id) await LoadLocalRepositoriesAsync();
            RepositoryStatus = $"ローカルリポジトリ {count} 件を確認しました。ファイルの変更やGit操作は行っていません。";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or KeyNotFoundException)
        {
            RepositoryStatus = exception.Message;
        }
    }

    private async Task LoadLocalRepositoriesAsync()
    {
        LocalRepositories.Clear();
        if (SelectedWorkspaceProfile is null) return;
        foreach (var repository in await _workspaceInventory.GetRepositoriesAsync(SelectedWorkspaceProfile.Id))
            LocalRepositories.Add(repository);
    }

    private async Task RefreshLocalRepositoriesSafelyAsync()
    {
        try { await LoadLocalRepositoriesAsync(); }
        catch (Exception) { RepositoryStatus = "ローカルの一覧を読み込めませんでした。"; }
    }

    private void BrowseWorkspaceRoot()
    {
        var dialog = new OpenFolderDialog { Title = "ワークスペースの保存先を選択", Multiselect = false };
        if (dialog.ShowDialog() == true) WorkspaceRootPath = dialog.FolderName;
    }

    private async Task GetGitStatusAsync()
    {
        if (!TryGetSelectedRepositoryPath(out var path)) return;
        try
        {
            GitSnapshot = await _gitOperations.GetStatusAsync(path);
            GitStatusText = FormatGitStatus(GitSnapshot);
            RefreshGitChangedPaths();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            GitSnapshot = null;
            GitChangedPaths.Clear();
            GitStatusText = exception.Message;
        }
    }

    private async Task CreateBranchAsync()
    {
        if (!TryGetSelectedRepositoryPath(out var path)) return;
        try
        {
            await _gitOperations.CreateBranchAsync(path, NewBranchName);
            GitStatusText = $"ブランチ {NewBranchName} を作成しました。";
            await GetGitStatusAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or TimeoutException)
        {
            GitStatusText = exception.Message;
        }
    }

    private async Task FetchAsync()
    {
        if (!TryGetSelectedRepositoryPath(out var path)) return;
        try
        {
            GitSnapshot = await _gitOperations.FetchAsync(path);
            GitStatusText = $"Fetch完了。{FormatGitStatus(GitSnapshot)}";
            RefreshGitChangedPaths();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            GitStatusText = exception.Message;
        }
    }

    private async Task PullFastForwardAsync()
    {
        if (!TryGetSelectedRepositoryPath(out var path) || GitSnapshot is null) return;
        var result = MessageBox.Show(
            $"{GitSnapshot.RepositoryFullName}\n{GitSnapshot.Branch} ← {GitSnapshot.Upstream}\n\n作業ツリーがcleanの場合のみ、fast-forwardで更新します。続けますか？",
            "Pullの確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            GitSnapshot = await _gitOperations.PullFastForwardAsync(path);
            GitStatusText = $"Fast-forward Pull完了。{FormatGitStatus(GitSnapshot)}";
            RefreshGitChangedPaths();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            GitStatusText = exception.Message;
        }
    }

    private async Task CommitStagedChangesAsync()
    {
        if (!TryGetSelectedRepositoryPath(out var path) || GitSnapshot is null) return;
        var result = MessageBox.Show(
            $"{GitSnapshot.RepositoryFullName}\nブランチ: {GitSnapshot.Branch}\nメッセージ: {CommitMessage}\n\nGitHub Desktop等でステージ済みの変更をコミットします。よろしいですか？",
            "コミットの確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            await _gitOperations.CommitStagedChangesAsync(path, CommitMessage);
            GitStatusText = "コミットしました。";
            await GetGitStatusAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or TimeoutException)
        {
            GitStatusText = exception.Message;
        }
    }

    private async Task PushAsync()
    {
        if (!TryGetSelectedRepositoryPath(out var path) || GitSnapshot is null) return;
        var result = MessageBox.Show(
            $"{GitSnapshot.RepositoryFullName}\noriginへ {GitSnapshot.Branch} をPushします。強制Pushは行いません。よろしいですか？",
            "Pushの確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            await _gitOperations.PushAsync(path);
            GitSnapshot = await _gitOperations.GetStatusAsync(path);
            GitStatusText = $"Push完了。{FormatGitStatus(GitSnapshot)}";
            RefreshGitChangedPaths();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            GitStatusText = "Pushできませんでした。認証、ブランチ保護、ネットワークを確認してください。";
        }
    }

    private async Task CreatePullRequestAsync()
    {
        if (!TryGetSelectedRepositoryPath(out var path) || GitSnapshot is null) return;
        var result = MessageBox.Show(
            $"{GitSnapshot.RepositoryFullName}\n{GitSnapshot.Branch} → {PullRequestBase}\n{PullRequestTitle}\n\nGitHubにPull Requestを作成します。よろしいですか？",
            "Pull Requestの確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            var pullRequest = await _pullRequestService.CreateAsync(path, PullRequestTitle, PullRequestBody, PullRequestBase);
            GitStatusText = $"PR #{pullRequest.Number}を作成しました: {pullRequest.HtmlUrl}";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or HttpRequestException or IOException)
        {
            GitStatusText = exception.Message;
        }
    }

    private bool TryGetSelectedRepositoryPath(out string path)
    {
        path = SelectedLocalRepository?.Path ?? "";
        if (string.IsNullOrEmpty(path)) GitStatusText = "先にローカルリポジトリを選択してください。";
        return !string.IsNullOrEmpty(path);
    }

    private void RefreshGitChangedPaths()
    {
        GitChangedPaths.Clear();
        if (GitSnapshot is null) return;
        foreach (var path in GitSnapshot.ChangedPaths) GitChangedPaths.Add(path);
    }

    private static string FormatGitStatus(GitRepositorySnapshot? snapshot)
    {
        if (snapshot is null) return "Git状態を取得できませんでした。";
        var state = snapshot.HasConflicts ? "競合あり" : snapshot.IsDirty ? $"変更 {snapshot.ChangedPaths.Count} 件" : "clean";
        return $"{snapshot.RepositoryFullName} · {snapshot.Branch} · {state} · ahead {snapshot.Ahead} / behind {snapshot.Behind}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class AsyncRelayCommand(Func<Task> execute) : ICommand
{
    private bool _isExecuting;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_isExecuting;
    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(); }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed record NavigationItem(string Key, string Title, ICommand NavigateCommand);

public sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter);

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
