using System.Net;
using System.Net.Mail;
using Application.Common.Interfaces;
using Domain.Enums;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly EmailSettings _emailSettings;

    public EmailService(ILogger<EmailService> logger, IOptions<EmailSettings> emailSettings)
    {
        _logger = logger;
        _emailSettings = emailSettings.Value;
    }

    public async Task<bool> SendEmailConfirmationAsync(string email, string confirmationLink)
    {
        const string systemNameEn = "SMA Retail System";
        const string systemNameAr = "Ù†Ø¸Ø§Ù… Ù‡ÙŠØ¦Ø© Ø§Ù„Ø´Ø§Ø±Ù‚Ø© Ù„Ù„Ù…ØªØ§Ø­Ù Ù„Ù„Ø¨ÙŠØ¹ Ø¨Ø§Ù„ØªØ¬Ø²Ø¦Ø©";
        const string accentColor = "#b91c1c";

        var subject = $"ØªØ£ÙƒÙŠØ¯ Ø§Ù„Ø¨Ø±ÙŠØ¯ Ø§Ù„Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ | Confirm Your Email - {systemNameEn}";
        var safeLink = WebUtility.HtmlEncode(confirmationLink);

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 720px; margin: 0 auto; padding: 20px;'>

                    <!-- Arabic section -->
                    <div dir='rtl' style='text-align:right;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>ØªØ£ÙƒÙŠØ¯ Ø§Ù„Ø¨Ø±ÙŠØ¯ Ø§Ù„Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameAr}</p>
                        <p>Ø´ÙƒØ±Ø§Ù‹ Ù„ØªØ³Ø¬ÙŠÙ„Ùƒ Ù…Ø¹Ù†Ø§.</p>
                        <p>ÙŠØ±Ø¬Ù‰ ØªØ£ÙƒÙŠØ¯ Ø¹Ù†ÙˆØ§Ù† Ø¨Ø±ÙŠØ¯Ùƒ Ø§Ù„Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ Ø¨Ø§Ù„Ø¶ØºØ· Ø¹Ù„Ù‰ Ø§Ù„Ø²Ø± Ø£Ø¯Ù†Ø§Ù‡:</p>
                        <div style='margin: 24px 0;'>
                            <a href='{confirmationLink}'
                               style='display: inline-block; padding: 12px 30px; background-color: {accentColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                ØªØ£ÙƒÙŠØ¯ Ø§Ù„Ø¨Ø±ÙŠØ¯ Ø§Ù„Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px'>Ø¥Ø°Ø§ Ù„Ù… ÙŠØ¹Ù…Ù„ Ø§Ù„Ø²Ø±ØŒ Ø§Ù†Ø³Ø® Ø§Ù„Ø±Ø§Ø¨Ø· Ø§Ù„ØªØ§Ù„ÙŠ ÙˆØ§Ù„ØµÙ‚Ù‡ ÙÙŠ Ø§Ù„Ù…ØªØµÙØ­:</p>
                        <p style='word-break: break-all; color: {accentColor}; font-size: 12px;' dir='ltr'>{safeLink}</p>
                        <p style='color: #666; margin-top: 16px'>Ø¥Ø°Ø§ Ù„Ù… ØªÙ‚Ù… Ø¨Ø¥Ù†Ø´Ø§Ø¡ Ø­Ø³Ø§Ø¨ØŒ ÙŠØ±Ø¬Ù‰ ØªØ¬Ø§Ù‡Ù„ Ù‡Ø°Ù‡ Ø§Ù„Ø±Ø³Ø§Ù„Ø©.</p>
                        <p style='color:#666;margin-top:24px'>Ù…Ø¹ Ø£Ø·ÙŠØ¨ Ø§Ù„ØªØ­ÙŠØ§ØªØŒ<br/>ÙØ±ÙŠÙ‚ {systemNameAr}</p>
                    </div>

                    <hr style='margin:32px 0;border:none;border-top:1px solid #e5e7eb' />

                    <!-- English section -->
                    <div dir='ltr' style='text-align:left;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>Email Confirmation</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameEn}</p>
                        <p>Thank you for registering.</p>
                        <p>Please confirm your email address by clicking the button below:</p>
                        <div style='margin: 24px 0;'>
                            <a href='{confirmationLink}'
                               style='display: inline-block; padding: 12px 30px; background-color: {accentColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                Confirm Email
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px'>If the button doesn't work, copy and paste this link into your browser:</p>
                        <p style='word-break: break-all; color: {accentColor}; font-size: 12px;'>{safeLink}</p>
                        <p style='color: #666; margin-top: 16px'>If you didn't create an account, please ignore this email.</p>
                        <p style='color:#666;margin-top:24px'>Best regards,<br/>{systemNameEn} Team</p>
                    </div>

                </div>
            </body>
            </html>
        ";

        return await SendEmailAsync(email, subject, body);
    }

    public async Task<bool> SendPasswordResetAsync(string email, string resetLink)
    {
        const string systemNameEn = "SMA Retail System";
        const string systemNameAr = "Ù†Ø¸Ø§Ù… Ù‡ÙŠØ¦Ø© Ø§Ù„Ø´Ø§Ø±Ù‚Ø© Ù„Ù„Ù…ØªØ§Ø­Ù Ù„Ù„Ø¨ÙŠØ¹ Ø¨Ø§Ù„ØªØ¬Ø²Ø¦Ø©";
        const string accentColor = "#b91c1c";

        var subject = $"Ø¥Ø¹Ø§Ø¯Ø© ØªØ¹ÙŠÙŠÙ† ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ± | Password Reset - {systemNameEn}";
        var safeLink = WebUtility.HtmlEncode(resetLink);

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 720px; margin: 0 auto; padding: 20px;'>

                    <!-- Arabic section -->
                    <div dir='rtl' style='text-align:right;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>Ø·Ù„Ø¨ Ø¥Ø¹Ø§Ø¯Ø© ØªØ¹ÙŠÙŠÙ† ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ±</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameAr}</p>
                        <p>Ù„Ù‚Ø¯ ØªÙ„Ù‚ÙŠÙ†Ø§ Ø·Ù„Ø¨Ø§Ù‹ Ù„Ø¥Ø¹Ø§Ø¯Ø© ØªØ¹ÙŠÙŠÙ† ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ± Ø§Ù„Ø®Ø§ØµØ© Ø¨Ùƒ.</p>
                        <p>Ø§Ø¶ØºØ· Ø¹Ù„Ù‰ Ø§Ù„Ø²Ø± Ø£Ø¯Ù†Ø§Ù‡ Ù„Ø¥Ø¹Ø§Ø¯Ø© ØªØ¹ÙŠÙŠÙ† ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ±:</p>
                        <div style='margin: 24px 0;'>
                            <a href='{resetLink}'
                               style='display: inline-block; padding: 12px 30px; background-color: {accentColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                Ø¥Ø¹Ø§Ø¯Ø© ØªØ¹ÙŠÙŠÙ† ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ±
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px'>Ø¥Ø°Ø§ Ù„Ù… ÙŠØ¹Ù…Ù„ Ø§Ù„Ø²Ø±ØŒ Ø§Ù†Ø³Ø® Ø§Ù„Ø±Ø§Ø¨Ø· Ø§Ù„ØªØ§Ù„ÙŠ ÙˆØ§Ù„ØµÙ‚Ù‡ ÙÙŠ Ø§Ù„Ù…ØªØµÙØ­:</p>
                        <p style='word-break: break-all; color: {accentColor}; font-size: 12px;' dir='ltr'>{safeLink}</p>
                        <p style='color: {accentColor}; margin-top: 16px;'><strong>ØªÙ†ØªÙ‡ÙŠ ØµÙ„Ø§Ø­ÙŠØ© Ù‡Ø°Ø§ Ø§Ù„Ø±Ø§Ø¨Ø· Ø®Ù„Ø§Ù„ 24 Ø³Ø§Ø¹Ø©.</strong></p>
                        <p style='color: #666; margin-top: 16px'>Ø¥Ø°Ø§ Ù„Ù… ØªØ·Ù„Ø¨ Ø¥Ø¹Ø§Ø¯Ø© ØªØ¹ÙŠÙŠÙ† ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ±ØŒ ÙŠØ±Ø¬Ù‰ ØªØ¬Ø§Ù‡Ù„ Ù‡Ø°Ù‡ Ø§Ù„Ø±Ø³Ø§Ù„Ø© Ø£Ùˆ Ø§Ù„ØªÙˆØ§ØµÙ„ Ù…Ø¹ Ø§Ù„Ø¯Ø¹Ù….</p>
                        <p style='color:#666;margin-top:24px'>Ù…Ø¹ Ø£Ø·ÙŠØ¨ Ø§Ù„ØªØ­ÙŠØ§ØªØŒ<br/>ÙØ±ÙŠÙ‚ {systemNameAr}</p>
                    </div>

                    <hr style='margin:32px 0;border:none;border-top:1px solid #e5e7eb' />

                    <!-- English section -->
                    <div dir='ltr' style='text-align:left;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>Password Reset Request</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameEn}</p>
                        <p>We received a request to reset your password.</p>
                        <p>Click the button below to reset your password:</p>
                        <div style='margin: 24px 0;'>
                            <a href='{resetLink}'
                               style='display: inline-block; padding: 12px 30px; background-color: {accentColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                Reset Password
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px'>If the button doesn't work, copy and paste this link into your browser:</p>
                        <p style='word-break: break-all; color: {accentColor}; font-size: 12px;'>{safeLink}</p>
                        <p style='color: {accentColor}; margin-top: 16px;'><strong>This link will expire in 24 hours.</strong></p>
                        <p style='color: #666; margin-top: 16px'>If you didn't request a password reset, please ignore this email or contact support.</p>
                        <p style='color:#666;margin-top:24px'>Best regards,<br/>{systemNameEn} Team</p>
                    </div>

                </div>
            </body>
            </html>
        ";

        return await SendEmailAsync(email, subject, body);
    }

    public async Task<bool> SendWelcomePasswordSetupAsync(string email, string resetLink)
    {
        const string systemNameEn = "SMA Retail System";
        const string systemNameAr = "Ù†Ø¸Ø§Ù… Ù‡ÙŠØ¦Ø© Ø§Ù„Ø´Ø§Ø±Ù‚Ø© Ù„Ù„Ù…ØªØ§Ø­Ù Ù„Ù„Ø¨ÙŠØ¹ Ø¨Ø§Ù„ØªØ¬Ø²Ø¦Ø©";
        const string accentColor = "#b91c1c";

        var subject = $"Ù…Ø±Ø­Ø¨Ø§Ù‹ - Ù‚Ù… Ø¨ØªØ¹ÙŠÙŠÙ† ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ± | Welcome - Set Your Password - {systemNameEn}";
        var safeLink = WebUtility.HtmlEncode(resetLink);

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 720px; margin: 0 auto; padding: 20px;'>

                    <!-- Arabic section -->
                    <div dir='rtl' style='text-align:right;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>Ù…Ø±Ø­Ø¨Ø§Ù‹</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameAr}</p>
                        <p>ØªÙ… Ø¥Ù†Ø´Ø§Ø¡ Ø­Ø³Ø§Ø¨ Ù„Ùƒ.</p>
                        <p>ÙŠØ±Ø¬Ù‰ ØªØ¹ÙŠÙŠÙ† ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ± Ø§Ù„Ø®Ø§ØµØ© Ø¨Ùƒ Ø¨Ø§Ù„Ø¶ØºØ· Ø¹Ù„Ù‰ Ø§Ù„Ø²Ø± Ø£Ø¯Ù†Ø§Ù‡:</p>
                        <div style='margin: 24px 0;'>
                            <a href='{resetLink}'
                               style='display: inline-block; padding: 12px 30px; background-color: {accentColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                ØªØ¹ÙŠÙŠÙ† ÙƒÙ„Ù…Ø© Ø§Ù„Ù…Ø±ÙˆØ±
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px'>Ø¥Ø°Ø§ Ù„Ù… ÙŠØ¹Ù…Ù„ Ø§Ù„Ø²Ø±ØŒ Ø§Ù†Ø³Ø® Ø§Ù„Ø±Ø§Ø¨Ø· Ø§Ù„ØªØ§Ù„ÙŠ ÙˆØ§Ù„ØµÙ‚Ù‡ ÙÙŠ Ø§Ù„Ù…ØªØµÙØ­:</p>
                        <p style='word-break: break-all; color: {accentColor}; font-size: 12px;' dir='ltr'>{safeLink}</p>
                        <p style='color: #b91c1c; margin-top: 16px;'><strong>ØªÙ†ØªÙ‡ÙŠ ØµÙ„Ø§Ø­ÙŠØ© Ù‡Ø°Ø§ Ø§Ù„Ø±Ø§Ø¨Ø· Ø®Ù„Ø§Ù„ 24 Ø³Ø§Ø¹Ø©.</strong></p>
                        <p style='color: #666; margin-top: 16px'>Ø¥Ø°Ø§ Ù„Ù… ØªÙƒÙ† ØªØªÙˆÙ‚Ø¹ Ù‡Ø°Ù‡ Ø§Ù„Ø±Ø³Ø§Ù„Ø©ØŒ ÙŠØ±Ø¬Ù‰ Ø§Ù„ØªÙˆØ§ØµÙ„ Ù…Ø¹ Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„.</p>
                        <p style='color:#666;margin-top:24px'>Ù…Ø¹ Ø£Ø·ÙŠØ¨ Ø§Ù„ØªØ­ÙŠØ§ØªØŒ<br/>ÙØ±ÙŠÙ‚ {systemNameAr}</p>
                    </div>

                    <hr style='margin:32px 0;border:none;border-top:1px solid #e5e7eb' />

                    <!-- English section -->
                    <div dir='ltr' style='text-align:left;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>Welcome</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameEn}</p>
                        <p>An account has been created for you.</p>
                        <p>Please set your password by clicking the button below:</p>
                        <div style='margin: 24px 0;'>
                            <a href='{resetLink}'
                               style='display: inline-block; padding: 12px 30px; background-color: {accentColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                Set Password
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px'>If the button doesn't work, copy and paste this link into your browser:</p>
                        <p style='word-break: break-all; color: {accentColor}; font-size: 12px;'>{safeLink}</p>
                        <p style='color: #b91c1c; margin-top: 16px;'><strong>This link will expire in 24 hours.</strong></p>
                        <p style='color: #666; margin-top: 16px'>If you didn't expect this email, please contact your administrator.</p>
                        <p style='color:#666;margin-top:24px'>Best regards,<br/>{systemNameEn} Team</p>
                    </div>

                </div>
            </body>
            </html>
        ";

        return await SendEmailAsync(email, subject, body);
    }


    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var host = _emailSettings.SmtpHost;
            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogWarning("Email settings not configured. Email to {To} not sent", to);
                return false;
            }

            var port = _emailSettings.SmtpPort;
            var username = _emailSettings.SmtpUsername;
            var password = _emailSettings.SmtpPassword;
            var fromAddress = _emailSettings.FromAddress;
            var fromName = _emailSettings.FromName;
            var enableSsl = _emailSettings.EnableSsl;

            // Validate required credentials
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Email credentials not configured. Email to {To} not sent", to);
                return false;
            }

            // Use username as fromAddress if not configured
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                fromAddress = username;
            }

            // Validate email addresses
            if (string.IsNullOrWhiteSpace(to) || !IsValidEmail(to))
            {
                _logger.LogError("Invalid recipient email address: {To}", to);
                return false;
            }

            if (!IsValidEmail(fromAddress))
            {
                fromAddress = username;
                if (!IsValidEmail(fromAddress))
                {
                    _logger.LogError("Invalid from email address. Username is also not a valid email");
                    return false;
                }
            }

            _logger.LogInformation("Sending email to {To} via {Host}:{Port}", to, host, port);

            // Accept self-signed certificates (required for internal mail servers)
#pragma warning disable SYSLIB0014
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) => true;
#pragma warning restore SYSLIB0014

            // Parse domain\username format for proper NTLM authentication
            NetworkCredential credential;
            if (username.Contains('\\'))
            {
                var parts = username.Split('\\', 2);
                credential = new NetworkCredential(parts[1], password, parts[0]);
            }
            else
            {
                credential = new NetworkCredential(username, password);
            }

            // IMPORTANT: UseDefaultCredentials must be set BEFORE Credentials,
            // otherwise setting UseDefaultCredentials resets Credentials to null.
            using var client = new SmtpClient(host, port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = credential,
                EnableSsl = enableSsl,
                Timeout = 30000
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(new MailAddress(to));

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {To}", to);
            return true;
        }
        catch (FormatException formatEx)
        {
            _logger.LogError(formatEx, "Invalid email address format when sending to {To}", to);
            return false;
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, "SMTP error sending email to {To}. Status: {Status}", to, smtpEx.StatusCode);
            
            if (smtpEx.StatusCode == SmtpStatusCode.MustIssueStartTlsFirst || 
                smtpEx.Message.Contains("5.7.0", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Gmail authentication failed. Ensure you are using an App Password, not your regular Gmail password. Generate one at: https://myaccount.google.com/apppasswords");
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            return false;
        }
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
