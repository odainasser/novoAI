namespace Domain.Exceptions;

public class RoleAssignedException : DomainException
{
    public RoleAssignedException(Guid roleId)
        : base($"Cannot delete role with ID '{roleId}' because there are users assigned to it.")
    {
    }

    public RoleAssignedException(string roleName)
        : base($"Cannot delete role '{roleName}' because there are users assigned to it.")
    {
    }
}
