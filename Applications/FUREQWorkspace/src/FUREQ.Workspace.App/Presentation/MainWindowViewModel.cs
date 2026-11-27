using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Net.Http;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.App.Presentation;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly RelayCommand _navigateCommand;
    private readonly IRepositoryDiscovery _repositoryDiscovery;
    private string _currentKey = "home";
    private string _repositoryStatus = "未取得";

    public MainWindowViewModel(IRepositoryDiscovery repositoryDiscovery)
    {
        _repositoryDiscovery = repositoryDiscovery;
        _navigateCommand = new RelayCommand(parameter => Navigate(parameter as string));
        LoadRepositoriesCommand = new AsyncRelayCommand(LoadRepositoriesAsync);
        Repositories = [];
        NavigationItems =
        [
            new NavigationItem("home", "ホーム", _navigateCommand),
            new NavigationItem("repositories", "リポジトリ", _navigateCommand),
            new NavigationItem("setup", "環境構築", _navigateCommand),
            new NavigationItem("settings", "設定", _navigateCommand)
        ];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public ObservableCollection<RepositoryMetadata> Repositories { get; }
    public ICommand LoadRepositoriesCommand { get; }
    public string CurrentKey => _currentKey;
    public string RepositoryStatus { get => _repositoryStatus; private set { _repositoryStatus = value; OnPropertyChanged(); } }

    public string DatabaseStatus => "ローカル設定データベースを初期化しました。認証情報は保存していません。";

    public string CurrentTitle => _currentKey switch
    {
        "repositories" => "リポジトリ",
        "setup" => "環境構築",
        "settings" => "設定",
        _ => "開発ワークスペース"
    };

    public string CurrentDescription => _currentKey switch
    {
        "repositories" => "FUREQ-DEVELOPのリポジトリを一覧し、ローカルの作業場所と状態を確認します。",
        "setup" => "必要な開発リポジトリとツールを選び、安全な手順でワークスペースを構築します。",
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
            var repositories = await _repositoryDiscovery.GetPublicRepositoriesAsync();
            Repositories.Clear();
            foreach (var repository in repositories)
            {
                Repositories.Add(repository);
            }

            RepositoryStatus = $"{Repositories.Count} 件の公開リポジトリを取得しました。";
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
