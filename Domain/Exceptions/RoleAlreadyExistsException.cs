namespace Domain.Exceptions;

public class RoleAlreadyExistsException : DomainException
{
    public RoleAlreadyExistsException(string roleName) 
        : base($"Role with name '{roleName}' already exists.")
    {
    }
}
