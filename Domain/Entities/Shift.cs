using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Shift : BaseAuditableEntity
{
    public Guid CashierId { get; set; }
    public string? CashierName { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public decimal TotalSales { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }

    public ShiftStatus Status { get; set; }

    public string? Comments { get; set; }

    public Guid? WarehouseId { get; set; }
    public string? WarehouseNameEn { get; set; }
    public string? WarehouseNameAr { get; set; }
}
