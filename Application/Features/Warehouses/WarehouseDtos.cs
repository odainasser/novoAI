namespace Application.Features.Warehouses;

public class WarehouseDto
{
    public Guid Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Email { get; set; }
    public Guid WarehouseTypeId { get; set; }
    public string? WarehouseTypeNameEn { get; set; }
    public string? WarehouseTypeNameAr { get; set; }
    public string? WarehouseTypeCode { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchNameEn { get; set; }
    public string? BranchNameAr { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateWarehouseRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Email { get; set; }
    public Guid WarehouseTypeId { get; set; }
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateWarehouseRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Email { get; set; }
    public Guid WarehouseTypeId { get; set; }
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; }
}
