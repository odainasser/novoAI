namespace Domain.Exceptions;

public class RoleNotFoundException : DomainException
{
    public RoleNotFoundException(Guid roleId) 
        : base($"Role with ID '{roleId}' was not found.")
    {
    }

    public RoleNotFoundException(string roleName) 
        : base($"Role with name '{roleName}' was not found.")
    {
    }
}
