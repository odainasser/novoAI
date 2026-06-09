using Web.Models.Enums;
using Web.Models.Common;

namespace Web.Models.Shifts;

public class ShiftDto
{
    public Guid Id { get; set; }
    public Guid CashierId { get; set; }
    public string? CashierName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    // ClosingBalance removed from client DTO; computed when needed
    public ShiftStatus Status { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseNameEn { get; set; }
    public string? WarehouseNameAr { get; set; }
}

public class StartShiftRequest
{
    public decimal CashIn { get; set; }
    public string? Comments { get; set; }
    // Offline replay only — actual open time captured at enqueue.
    public DateTime? ClientStartedAt { get; set; }
}

public class EndShiftRequest
{
    public decimal CashOut { get; set; }
    // ClosingBalance no longer submitted by the client
    public string? Comments { get; set; }
    // Offline replay only — actual close time captured at enqueue.
    public DateTime? ClientEndedAt { get; set; }
}
