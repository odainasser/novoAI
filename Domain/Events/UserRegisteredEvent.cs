namespace Domain.Events;

public class UserRegisteredEvent : DomainEvent
{
    public Guid UserId { get; }
    public string Email { get; }
    public string? FirstName { get; }
    public string? LastName { get; }

    public UserRegisteredEvent(Guid userId, string email, string? firstName, string? lastName)
    {
        UserId = userId;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
    }
}
