using Domain.Enums;

namespace Application.Features.UserLogs;

public class CreateUserLogRequest
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
}
