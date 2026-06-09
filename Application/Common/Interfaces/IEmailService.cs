namespace Application.Common.Interfaces;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string to, string subject, string body);
    Task<bool> SendEmailConfirmationAsync(string email, string confirmationLink);
    Task<bool> SendPasswordResetAsync(string email, string resetLink);
    Task<bool> SendWelcomePasswordSetupAsync(string email, string resetLink);
}
