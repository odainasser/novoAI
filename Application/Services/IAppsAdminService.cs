using Application.Features.Apps;

namespace Application.Services;

/// <summary>
/// Management surface for the Apps integration module: other systems integrate
/// WITH ByteAI by being registered here (code + tool-provider base URL + persona).
/// Onboarding an app is data entry, never a ByteAI deployment.
/// </summary>
public interface IAppsAdminService
{
    Task<IReadOnlyList<AppDto>> GetAppsAsync(CancellationToken cancellationToken = default);

    Task<AppDto?> GetAppAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Register a new app. Throws <see cref="ArgumentException"/> on an
    /// invalid/duplicate code or an invalid base URL.</summary>
    Task<AppDto> CreateAppAsync(SaveAppRequest request, CancellationToken cancellationToken = default);

    Task UpdateAppAsync(Guid id, SaveAppRequest request, CancellationToken cancellationToken = default);

    /// <summary>Activate/deactivate without deleting (inactive apps are refused service).</summary>
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
