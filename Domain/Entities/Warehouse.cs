using Domain.Common;

namespace Domain.Entities;

public class Warehouse : BaseAuditableEntity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// FK to Lookup (child of WAREHOUSE_TYPE root).
    /// </summary>
    public Guid WarehouseTypeId { get; set; }
    public virtual Lookup WarehouseType { get; set; } = null!;

    /// <summary>
    /// Required only when the warehouse type is "Branch Warehouse" (code MW).
    /// </summary>
    public Guid? BranchId { get; set; }
    public virtual Branch? Branch { get; set; }

    public bool IsActive { get; set; } = true;
}
