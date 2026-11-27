using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;
using FUREQ.Workspace.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class WorkspaceSetupServiceTests
{
    [Fact]
    public async Task Setup_plan_and_per_item_progress_survive_database_reopen()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"fureq-setup-state-{Guid.NewGuid():N}.db");
        try
        {
            var database = new WorkspaceDatabase(databasePath);
            await new DatabaseInitializer(database).InitializeAsync(TestContext.Current.CancellationToken);
            var store = new SqliteSetupProgressStore(database);
            var profileId = Guid.NewGuid();
            var plan = new WorkspaceSetupPlan(Guid.NewGuid(), profileId, "C:\\workspace", WorkspaceSetupMode.Full,
                LfsDownloadMode.Development, DateTimeOffset.UtcNow, []);
            await store.SavePlanAsync(plan, TestContext.Current.CancellationToken);
            await store.SaveAsync(new SetupItemProgress(plan.Id, "FUREQ-DEVELOP/RenderingWorkspace", "C:\\workspace\\Rendering\\RenderingWorkspace",
                SetupItemState.Completed, "done", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

            var reopened = new SqliteSetupProgressStore(new WorkspaceDatabase(databasePath));
            Assert.Equal(plan.Id, (await reopened.GetLatestPlanAsync(profileId, TestContext.Current.CancellationToken))!.Id);
            Assert.Equal(SetupItemState.Completed, Assert.Single(await reopened.GetAsync(plan.Id, TestContext.Current.CancellationToken)).State);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                var path = databasePath + suffix;
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Full_plan_uses_versioned_layout_and_custom_selection_expands_dependencies()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var profile = CreateProfile(Path.Combine(root, "workspace"));
            Directory.CreateDirectory(profile.RootPath);
            var service = CreateService(new GitProcessRunner(), new MemoryProgressStore());
            var metadata = CreateMetadata("Game-001-Code", 1, "C:\\repos\\Game-001-Code.git");
            var full = await service.CreatePlanAsync(profile, WorkspaceSetupMode.Full, [metadata], LfsDownloadMode.Development,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(14, full.Items.Count);
            Assert.Equal(13, full.Blockers.Count);
            Assert.Single(full.Items, item => item.RelativePath == "Games/Game-001/Game-001-Code");

            var custom = await service.CreatePlanAsync(profile, WorkspaceSetupMode.Custom, [metadata], LfsDownloadMode.Development,
                ["Game-001-Code"], TestContext.Current.CancellationToken);
            Assert.Contains(custom.Items, item => item.FullName.EndsWith("/Game-001", StringComparison.Ordinal));
            Assert.Contains(custom.Items, item => item.FullName.EndsWith("/GameEngine", StringComparison.Ordinal));
            Assert.Contains(custom.Items, item => item.FullName.EndsWith("/DevelopmentTools", StringComparison.Ordinal));
        }
        finally { DeleteTemporaryDirectory(root); }
    }

    [Fact]
    public async Task Clones_plan_with_at_most_three_workers_and_restores_completed_plan_for_resume()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var profile = CreateProfile(Path.Combine(root, "workspace"));
            Directory.CreateDirectory(profile.RootPath);
            var runner = new GitProcessRunner();
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            await RunGitAsync(runner, source, "init", "--initial-branch=main");
            await RunGitAsync(runner, source, "config", "user.name", "FUREQ Test");
            await RunGitAsync(runner, source, "config", "user.email", "fureq-test@example.invalid");
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "setup fixture", TestContext.Current.CancellationToken);
            await RunGitAsync(runner, source, "add", "--", "README.md");
            await RunGitAsync(runner, source, "commit", "--message", "fixture");

            var repositories = new List<RepositoryMetadata>();
            foreach (var (name, id) in new[] { ("RenderingWorkspace", 1L), ("RenderingLibrary", 2L), ("DevelopmentTools", 3L), ("DevelopmentDocs", 4L) })
            {
                var remote = Path.Combine(root, name + ".git");
                await RunGitAsync(runner, root, "clone", "--bare", source, remote);
                repositories.Add(CreateMetadata(name, id, remote));
            }

            var store = new MemoryProgressStore();
            var trackingRunner = new TrackingCloneRunner(runner);
            var service = CreateService(trackingRunner, store);
            var plan = await service.CreatePlanAsync(profile, WorkspaceSetupMode.Rendering, repositories, LfsDownloadMode.Development,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Empty(plan.Blockers);
            Assert.Equal(4, plan.CloneItems.Count);
            using var cancellation = new CancellationTokenSource();
            var cancelAfterFirstCompletion = new CancelAfterFirstCompletedItem(cancellation);
            var partial = await service.ExecuteAsync(plan, cancelAfterFirstCompletion, cancellation.Token);
            Assert.Contains(partial.Items, item => item.State == SetupItemState.Completed);
            Assert.Contains(partial.Items, item => item.State == SetupItemState.Cancelled);

            var resumed = await service.GetLatestPlanAsync(profile.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(resumed);
            var completed = await service.ExecuteAsync(resumed, cancellationToken: TestContext.Current.CancellationToken);
            Assert.All(completed.Items, item => Assert.True(item.State is SetupItemState.Completed or SetupItemState.Reuse));
            Assert.All(completed.Items, item => Assert.True(File.Exists(Path.Combine(item.TargetPath, ".git"))
                || Directory.Exists(Path.Combine(item.TargetPath, ".git"))));
            Assert.InRange(trackingRunner.MaximumConcurrentClones, 2, 3);
        }
        finally { DeleteTemporaryDirectory(root); }
    }

    private static WorkspaceSetupService CreateService(IGitProcessRunner runner, ISetupProgressStore store) =>
        new(runner, new LocalRemotePolicy(), new GitSubmoduleService(runner, new LocalRemotePolicy()), store);

    private static WorkspaceProfile CreateProfile(string root) =>
        new(Guid.NewGuid(), "Test", "Rendering", root, DateTimeOffset.UtcNow);

    private static RepositoryMetadata CreateMetadata(string name, long id, string cloneUrl) =>
        new(id, name, $"FUREQ-DEVELOP/{name}", null, $"https://github.com/FUREQ-DEVELOP/{name}", cloneUrl,
            "main", "public", false, false, DateTimeOffset.UtcNow);

    private static async Task RunGitAsync(IGitProcessRunner runner, string path, params string[] arguments)
    {
        var result = await runner.RunAsync(path, arguments, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, $"git exited {result.ExitCode}: {result.StandardError}");
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fureq-setup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }

    private sealed class LocalRemotePolicy : IGitRemotePolicy
    {
        public string GetRepositoryFullName(string remoteUrl)
        {
            var name = Path.GetFileNameWithoutExtension(remoteUrl.TrimEnd(Path.DirectorySeparatorChar));
            return $"FUREQ-DEVELOP/{name}";
        }
    }

    private sealed class MemoryProgressStore : ISetupProgressStore
    {
        private readonly Dictionary<Guid, WorkspaceSetupPlan> _plans = [];
        private readonly Dictionary<(Guid Plan, string Name), SetupItemProgress> _items = [];

        public Task SavePlanAsync(WorkspaceSetupPlan plan, CancellationToken cancellationToken = default)
        {
            _plans[plan.Id] = plan;
            return Task.CompletedTask;
        }
        public Task<WorkspaceSetupPlan?> GetLatestPlanAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_plans.Values.Where(plan => plan.WorkspaceProfileId == profileId).OrderByDescending(plan => plan.CreatedAtUtc).FirstOrDefault());
        public Task SaveAsync(SetupItemProgress progress, CancellationToken cancellationToken = default)
        {
            _items[(progress.PlanId, progress.FullName)] = progress;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<SetupItemProgress>> GetAsync(Guid planId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SetupItemProgress>>(_items.Values.Where(item => item.PlanId == planId).ToArray());
    }

    private sealed class CancelAfterFirstCompletedItem(CancellationTokenSource cancellation) : IProgress<SetupItemProgress>
    {
        private int _hasCancelled;
        public void Report(SetupItemProgress value)
        {
            if (value.State == SetupItemState.Completed && Interlocked.Exchange(ref _hasCancelled, 1) == 0)
                cancellation.Cancel();
        }
    }

    private sealed class TrackingCloneRunner(IGitProcessRunner inner) : IGitProcessRunner
    {
        private int _activeClones;
        private int _maximumConcurrentClones;
        public int MaximumConcurrentClones => _maximumConcurrentClones;

        public async Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
            TimeSpan timeout, CancellationToken cancellationToken = default, bool disableHooks = false,
            bool disableFsMonitor = false, bool useGitCredentialManager = false, bool isolateCheckoutConfiguration = false)
        {
            var isClone = arguments.Count > 0 && arguments[0] == "clone";
            if (isClone)
            {
                var active = Interlocked.Increment(ref _activeClones);
                UpdateMaximum(active);
                await Task.Delay(100, cancellationToken);
            }
            try
            {
                return await inner.RunAsync(workingDirectory, arguments, timeout, cancellationToken,
                    disableHooks, disableFsMonitor, useGitCredentialManager, isolateCheckoutConfiguration);
            }
            finally { if (isClone) Interlocked.Decrement(ref _activeClones); }
        }

        public Task<string?> GetGitHubCredentialUsernameAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
            inner.GetGitHubCredentialUsernameAsync(workingDirectory, cancellationToken);

        private void UpdateMaximum(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maximumConcurrentClones);
                if (active <= current || Interlocked.CompareExchange(ref _maximumConcurrentClones, active, current) == current) return;
            }
        }
    }
}
