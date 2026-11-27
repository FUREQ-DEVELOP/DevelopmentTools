using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface IAppSettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(AppSetting setting, CancellationToken cancellationToken = default);
}
