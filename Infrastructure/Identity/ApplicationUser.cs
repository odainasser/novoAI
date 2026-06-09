using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;

    // Mark certain seeded users as system users so they cannot be deleted/modified by normal UI
    public bool IsSystemUser { get; set; } = false;

    // Warehouse/Store assignment (required for Cashier role users)
    public Guid? WarehouseId { get; set; }

    // Whether this cashier is allowed to perform refunds
    public bool CanRefund { get; set; } = true;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}