using Web.Models.Apps;

namespace Web.Services;

/// <summary>Client for the Apps integration module (/api/apps).</summary>
public interface IAppsClientService
{
    Task<List<AppDto>> GetAppsAsync(CancellationToken cancellationToken = default);
    Task<AppDto> CreateAppAsync(SaveAppRequest request, CancellationToken cancellationToken = default);
    Task UpdateAppAsync(Guid id, SaveAppRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
