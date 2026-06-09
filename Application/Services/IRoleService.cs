using Application.Common.Models;
using Application.Features.Roles;

namespace Application.Services;

public interface IRoleService
{
    Task<PaginatedList<RoleResponse>> GetAllRolesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<RoleDetailResponse?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleResponse> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleDetailResponse> AssignPermissionsToRoleAsync(Guid roleId, AssignPermissionsRequest request, CancellationToken cancellationToken = default);
    Task<bool> CheckRoleNameExistsAsync(string name, Guid? excludeRoleId = null, CancellationToken cancellationToken = default);
}
