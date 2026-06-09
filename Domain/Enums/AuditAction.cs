namespace Domain.Enums;

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    LoggedIn = 4,
    LoggedOut = 5,
    PasswordChanged = 6,
    PasswordReset = 7,
    EmailVerified = 8,
    TwoFactorEnabled = 9,
    TwoFactorDisabled = 10,
    UpdatedDraft = 11,
    RequestedUpdate = 12,
    SetSellingDetails = 13,
    SetLogisticsDetails = 14,
    RequestedSetSellingDetails = 15,
    RequestedActivation = 16,
    RequestedDeletion = 17,
    RequestedSetLogisticsDetails = 18,
    UpdatedRequest = 19,
    ApprovedRequest = 20,
    RejectedRequest = 21
}
