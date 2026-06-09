namespace Domain.Events;

public class UserLoggedInEvent : DomainEvent
{
    public Guid UserId { get; }
    public string Email { get; }
    public string? UserAgent { get; }

    public UserLoggedInEvent(Guid userId, string email, string? userAgent = null)
    {
        UserId = userId;
        Email = email;
        UserAgent = userAgent;
    }
}
