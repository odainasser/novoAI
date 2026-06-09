namespace Domain.Exceptions;

public class SystemUserModificationException : DomainException
{
    public SystemUserModificationException()
        : base("Cannot modify or delete system users.")
    {
    }

    public SystemUserModificationException(string message)
        : base(message)
    {
    }
}
