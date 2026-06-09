using Web.Models.Roles;

namespace Web.Services;

public interface IPermissionManagementService
{
    Task<IEnumerable<PermissionDto>> GetAllPermissionsAsync();
    Task<IEnumerable<PermissionDto>> GetPermissionsByRoleIdAsync(Guid roleId);
}
