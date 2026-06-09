using Domain.Enums;

namespace Application.Features.Shifts;

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
    // ClosingBalance removed from DTO, it's a computed value at end-shift time
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
    // UTC time the shift was opened on the client. Used by offline-replayed
    // shifts so the server records the actual start time, not the sync time.
    public DateTime? ClientStartedAt { get; set; }
}

public class EndShiftRequest
{
    public decimal CashOut { get; set; }
    // ClosingBalance no longer provided by client; server computes expected closing and validates
    public string? Comments { get; set; }
    // UTC time the shift was closed on the client. Same purpose as ClientStartedAt.
    public DateTime? ClientEndedAt { get; set; }
}
