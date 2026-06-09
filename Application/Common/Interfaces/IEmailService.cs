using Domain.Enums;

namespace Application.Common.Interfaces;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string to, string subject, string body);
    Task<bool> SendEmailConfirmationAsync(string email, string confirmationLink);
    Task<bool> SendPasswordResetAsync(string email, string resetLink);
    Task<bool> SendWelcomePasswordSetupAsync(string email, string resetLink);
    Task<bool> SendLowStockAlertAsync(
        IEnumerable<string> recipients,
        IEnumerable<LowStockAlertItem> items,
        string? triggeredByEmail = null,
        string? warehouseNameEn = null,
        string? warehouseNameAr = null);
    Task<bool> SendRequestActionAsync(
        string recipientEmail,
        string requestedByName,
        RequestType requestType,
        bool approved,
        string reviewerName,
        string? reviewNote,
        string? subjectName,
        DateTime decidedAtUtc);
}

public class LowStockAlertItem
{
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int RemainingQuantity { get; set; }
    public int LowStockThreshold { get; set; }
    public List<LowStockAlertUnitItem> Units { get; set; } = new();
}

public class LowStockAlertUnitItem
{
    public string UnitNameEn { get; set; } = string.Empty;
    public string UnitNameAr { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int RemainingQuantity { get; set; }
    public int LowStockThreshold { get; set; }
}
