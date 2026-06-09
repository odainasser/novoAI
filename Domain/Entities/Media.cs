using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Media : BaseAuditableEntity
{
    public EntityType EntityType { get; set; } = EntityType.Unknown;
    public Guid EntityId { get; set; }
    public string CollectionName { get; set; } = "default";
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Disk { get; set; } = "local";
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public int Order { get; set; }
    public bool IsMain { get; set; }
}
