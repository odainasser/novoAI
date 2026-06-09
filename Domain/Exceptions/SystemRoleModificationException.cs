namespace Domain.Exceptions;

public class SystemRoleModificationException : DomainException
{
    public SystemRoleModificationException() 
        : base("Cannot modify or delete system roles.")
    {
    }

    public SystemRoleModificationException(string message) 
        : base(message)
    {
    }
}
