namespace Application.Features.Roles;

public class AssignPermissionsRequest
{
    public List<Guid> PermissionIds { get; set; } = new();
}
