using Web.Models.Common;
using Web.Models.Roles;

namespace Web.Services;

public interface IRoleManagementService
{
    Task<PaginatedList<RoleResponse>> GetAllRolesAsync(int pageNumber, int pageSize);
    Task<RoleDetailResponse?> GetRoleByIdAsync(Guid roleId);
    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request);
    Task<RoleResponse> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request);
    Task DeleteRoleAsync(Guid roleId);
    Task<RoleDetailResponse> AssignPermissionsToRoleAsync(Guid roleId, AssignPermissionsRequest request);
    Task<bool> CheckRoleNameExistsAsync(string name, Guid? excludeRoleId = null);
}
