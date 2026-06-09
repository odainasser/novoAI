using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class UserLog : BaseEntity
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty; // Snapshot of username
    public AuditAction Action { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; } // JSON or text
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
