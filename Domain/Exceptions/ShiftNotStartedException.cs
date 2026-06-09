namespace Domain.Exceptions;

public class ShiftNotStartedException : DomainException
{
    public ShiftNotStartedException()
        : base("Please start your shift before creating orders.")
    {
    }

    public ShiftNotStartedException(string message) : base(message)
    {
    }
}
