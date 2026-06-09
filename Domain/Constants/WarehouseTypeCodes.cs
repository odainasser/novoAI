namespace Domain.Constants;

// Lookup codes for the WAREHOUSE_TYPE category seeded in LookupSeeder.
// These strings are the source of truth referenced from EF queries and
// service-level dispatch logic; the Lookup rows themselves are seeded
// against the same literal values.
public static class WarehouseTypeCodes
{
    public const string CentralWarehouse = "CW";
    public const string BranchStore = "MS";
    public const string BranchWarehouse = "MW";
}
