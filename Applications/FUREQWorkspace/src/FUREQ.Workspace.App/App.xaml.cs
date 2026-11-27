using FUREQ.Workspace.Application;
using FUREQ.Workspace.Infrastructure;
using FUREQ.Workspace.App.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Windows;

namespace FUREQ.Workspace.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<WorkspaceDatabase>();
                services.AddSingleton<DatabaseInitializer>();
                services.AddSingleton<IAppSettingsRepository, SqliteAppSettingsRepository>();
                var githubClient = new HttpClient
                {
                    BaseAddress = new Uri("https://api.github.com/"),
                    Timeout = TimeSpan.FromSeconds(30)
                };
                githubClient.DefaultRequestHeaders.UserAgent.ParseAdd("FUREQ-Workspace/0.1");
                services.AddSingleton(githubClient);
                services.AddSingleton<IRepositoryDiscovery, GitHubPublicRepositoryDiscovery>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        try
        {
            await _host.StartAsync();
            await _host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            _host.Services.GetRequiredService<ILogger<App>>().LogCritical(exception, "Application startup failed.");
            MessageBox.Show(
                "FUREQ Workspaceを起動できませんでした。ログを確認してください。",
                "起動エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
