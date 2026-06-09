namespace Application.Features.UserLogs;

public class UserLogDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? EntityDisplayName { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}
