using Application.Features.CashierOffline;

namespace Application.Services;

public interface ICashierOfflineService
{
    // Builds the full offline payload for the cashier: credential metadata,
    // profile, assigned stores, all assigned-store products with image ETag,
    // current and recent shifts, and orders from the last `orderHistoryDays` days.
    Task<CashierOfflineDataResponse?> GetOfflineDataAsync(Guid userId, int orderHistoryDays = 30, int credentialLifetimeDays = 7);
}
