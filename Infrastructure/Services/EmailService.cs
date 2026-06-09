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
        const string systemNameAr = "نظام هيئة الشارقة للمتاحف للبيع بالتجزئة";
        const string accentColor = "#b91c1c";

        var subject = $"تأكيد البريد الإلكتروني | Confirm Your Email - {systemNameEn}";
        var safeLink = WebUtility.HtmlEncode(confirmationLink);

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 720px; margin: 0 auto; padding: 20px;'>

                    <!-- Arabic section -->
                    <div dir='rtl' style='text-align:right;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>تأكيد البريد الإلكتروني</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameAr}</p>
                        <p>شكراً لتسجيلك معنا.</p>
                        <p>يرجى تأكيد عنوان بريدك الإلكتروني بالضغط على الزر أدناه:</p>
                        <div style='margin: 24px 0;'>
                            <a href='{confirmationLink}'
                               style='display: inline-block; padding: 12px 30px; background-color: {accentColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                تأكيد البريد الإلكتروني
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px'>إذا لم يعمل الزر، انسخ الرابط التالي والصقه في المتصفح:</p>
                        <p style='word-break: break-all; color: {accentColor}; font-size: 12px;' dir='ltr'>{safeLink}</p>
                        <p style='color: #666; margin-top: 16px'>إذا لم تقم بإنشاء حساب، يرجى تجاهل هذه الرسالة.</p>
                        <p style='color:#666;margin-top:24px'>مع أطيب التحيات،<br/>فريق {systemNameAr}</p>
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
        const string systemNameAr = "نظام هيئة الشارقة للمتاحف للبيع بالتجزئة";
        const string accentColor = "#b91c1c";

        var subject = $"إعادة تعيين كلمة المرور | Password Reset - {systemNameEn}";
        var safeLink = WebUtility.HtmlEncode(resetLink);

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 720px; margin: 0 auto; padding: 20px;'>

                    <!-- Arabic section -->
                    <div dir='rtl' style='text-align:right;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>طلب إعادة تعيين كلمة المرور</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameAr}</p>
                        <p>لقد تلقينا طلباً لإعادة تعيين كلمة المرور الخاصة بك.</p>
                        <p>اضغط على الزر أدناه لإعادة تعيين كلمة المرور:</p>
                        <div style='margin: 24px 0;'>
                            <a href='{resetLink}'
                               style='display: inline-block; padding: 12px 30px; background-color: {accentColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                إعادة تعيين كلمة المرور
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px'>إذا لم يعمل الزر، انسخ الرابط التالي والصقه في المتصفح:</p>
                        <p style='word-break: break-all; color: {accentColor}; font-size: 12px;' dir='ltr'>{safeLink}</p>
                        <p style='color: {accentColor}; margin-top: 16px;'><strong>تنتهي صلاحية هذا الرابط خلال 24 ساعة.</strong></p>
                        <p style='color: #666; margin-top: 16px'>إذا لم تطلب إعادة تعيين كلمة المرور، يرجى تجاهل هذه الرسالة أو التواصل مع الدعم.</p>
                        <p style='color:#666;margin-top:24px'>مع أطيب التحيات،<br/>فريق {systemNameAr}</p>
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
        const string systemNameAr = "نظام هيئة الشارقة للمتاحف للبيع بالتجزئة";
        const string accentColor = "#b91c1c";

        var subject = $"مرحباً - قم بتعيين كلمة المرور | Welcome - Set Your Password - {systemNameEn}";
        var safeLink = WebUtility.HtmlEncode(resetLink);

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 720px; margin: 0 auto; padding: 20px;'>

                    <!-- Arabic section -->
                    <div dir='rtl' style='text-align:right;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>مرحباً</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameAr}</p>
                        <p>تم إنشاء حساب لك.</p>
                        <p>يرجى تعيين كلمة المرور الخاصة بك بالضغط على الزر أدناه:</p>
                        <div style='margin: 24px 0;'>
                            <a href='{resetLink}'
                               style='display: inline-block; padding: 12px 30px; background-color: {accentColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                تعيين كلمة المرور
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px'>إذا لم يعمل الزر، انسخ الرابط التالي والصقه في المتصفح:</p>
                        <p style='word-break: break-all; color: {accentColor}; font-size: 12px;' dir='ltr'>{safeLink}</p>
                        <p style='color: #b91c1c; margin-top: 16px;'><strong>تنتهي صلاحية هذا الرابط خلال 24 ساعة.</strong></p>
                        <p style='color: #666; margin-top: 16px'>إذا لم تكن تتوقع هذه الرسالة، يرجى التواصل مع المسؤول.</p>
                        <p style='color:#666;margin-top:24px'>مع أطيب التحيات،<br/>فريق {systemNameAr}</p>
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

    public async Task<bool> SendLowStockAlertAsync(
        IEnumerable<string> recipients,
        IEnumerable<LowStockAlertItem> items,
        string? triggeredByEmail = null,
        string? warehouseNameEn = null,
        string? warehouseNameAr = null)
    {
        const string systemNameEn = "SMA Retail System";
        const string systemNameAr = "نظام هيئة الشارقة للمتاحف للبيع بالتجزئة";

        var itemList = items?.ToList() ?? new List<LowStockAlertItem>();
        var recipientList = (recipients ?? Enumerable.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (itemList.Count == 0 || recipientList.Count == 0)
        {
            return false;
        }

        var storeSuffixEn = string.IsNullOrWhiteSpace(warehouseNameEn) ? string.Empty : $" at {warehouseNameEn}";
        var storeSuffixAr = string.IsNullOrWhiteSpace(warehouseNameAr) ? string.Empty : $" في {warehouseNameAr}";
        var subject = itemList.Count == 1
            ? $"تنبيه انخفاض المخزون: {itemList[0].ProductNameAr}{storeSuffixAr} | Low stock alert: {itemList[0].ProductNameEn}{storeSuffixEn} - {systemNameEn}"
            : $"تنبيه انخفاض المخزون: {itemList.Count} منتجات{storeSuffixAr} | Low stock alert: {itemList.Count} products{storeSuffixEn} - {systemNameEn}";

        // Flatten: one row per low unit so the unit (UoM) is shown explicitly per line.
        var flatRows = itemList.SelectMany(i => i.Units.Select(u => new
        {
            ProductNameEn = i.ProductNameEn,
            ProductNameAr = string.IsNullOrWhiteSpace(i.ProductNameAr) ? i.ProductNameEn : i.ProductNameAr,
            ProductCode = i.ProductCode,
            UnitNameEn = u.UnitNameEn,
            UnitNameAr = string.IsNullOrWhiteSpace(u.UnitNameAr) ? u.UnitNameEn : u.UnitNameAr,
            Barcode = u.Barcode,
            Remaining = u.RemainingQuantity,
            Threshold = u.LowStockThreshold
        })).ToList();

        var rowsEn = string.Join("", flatRows.Select(r => $@"
                        <tr>
                            <td style='padding:8px;border:1px solid #e5e7eb'>{WebUtility.HtmlEncode(r.ProductNameEn)}</td>
                            <td style='padding:8px;border:1px solid #e5e7eb'>{WebUtility.HtmlEncode(r.UnitNameEn)}</td>
                            <td style='padding:8px;border:1px solid #e5e7eb'>{WebUtility.HtmlEncode(r.Barcode)}</td>
                            <td style='padding:8px;border:1px solid #e5e7eb;text-align:right;color:#b91c1c;font-weight:bold'>{r.Remaining}</td>
                            <td style='padding:8px;border:1px solid #e5e7eb;text-align:right'>{r.Threshold}</td>
                        </tr>"));

        var rowsAr = string.Join("", flatRows.Select(r => $@"
                        <tr>
                            <td style='padding:8px;border:1px solid #e5e7eb'>{WebUtility.HtmlEncode(r.ProductNameAr)}</td>
                            <td style='padding:8px;border:1px solid #e5e7eb'>{WebUtility.HtmlEncode(r.UnitNameAr)}</td>
                            <td style='padding:8px;border:1px solid #e5e7eb'>{WebUtility.HtmlEncode(r.Barcode)}</td>
                            <td style='padding:8px;border:1px solid #e5e7eb;text-align:left;color:#b91c1c;font-weight:bold'>{r.Remaining}</td>
                            <td style='padding:8px;border:1px solid #e5e7eb;text-align:left'>{r.Threshold}</td>
                        </tr>"));

        var warehouseLineEn = string.IsNullOrWhiteSpace(warehouseNameEn)
            ? string.Empty
            : $" at <strong>{WebUtility.HtmlEncode(warehouseNameEn)}</strong>";
        var warehouseLineAr = string.IsNullOrWhiteSpace(warehouseNameAr)
            ? string.Empty
            : $" في <strong>{WebUtility.HtmlEncode(warehouseNameAr)}</strong>";

        var triggeredBy = WebUtility.HtmlEncode(triggeredByEmail ?? "N/A");

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 720px; margin: 0 auto; padding: 20px;'>

                    <!-- Arabic section -->
                    <div dir='rtl' style='text-align:right;'>
                        <h2 style='color: #b91c1c; margin-bottom: 4px;'>تنبيه انخفاض المخزون</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameAr}</p>
                        <p>وصلت المنتجات التالية أو انخفضت إلى ما دون الحد الأدنى المُعرَّف للمخزون بعد عملية بيع حديثة{warehouseLineAr}.</p>
                        <table style='border-collapse:collapse;width:100%;margin-top:12px'>
                            <thead>
                                <tr style='background:#f3f4f6'>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:right'>المنتج</th>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:right'>الوحدة</th>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:right'>الباركود</th>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:left'>المتبقي</th>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:left'>الحد الأدنى</th>
                                </tr>
                            </thead>
                            <tbody>{rowsAr}</tbody>
                        </table>
                        <p style='margin-top:16px;color:#666;font-size:13px'>تم التنبيه بواسطة الكاشير: {triggeredBy}</p>
                        <p style='color:#666;margin-top:24px'>مع أطيب التحيات،<br/>فريق {systemNameAr}</p>
                    </div>

                    <hr style='margin:32px 0;border:none;border-top:1px solid #e5e7eb' />

                    <!-- English section -->
                    <div dir='ltr' style='text-align:left;'>
                        <h2 style='color: #b91c1c; margin-bottom: 4px;'>Low Stock Alert</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameEn}</p>
                        <p>The following product(s) have reached or fallen below the configured low-stock threshold after a recent sale{warehouseLineEn}.</p>
                        <table style='border-collapse:collapse;width:100%;margin-top:12px'>
                            <thead>
                                <tr style='background:#f3f4f6'>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:left'>Product</th>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:left'>Unit</th>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:left'>Barcode</th>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:right'>Remaining</th>
                                    <th style='padding:8px;border:1px solid #e5e7eb;text-align:right'>Threshold</th>
                                </tr>
                            </thead>
                            <tbody>{rowsEn}</tbody>
                        </table>
                        <p style='margin-top:16px;color:#666;font-size:13px'>Triggered by cashier: {triggeredBy}</p>
                        <p style='color:#666;margin-top:24px'>Best regards,<br/>{systemNameEn} Team</p>
                    </div>

                </div>
            </body>
            </html>
        ";

        var allOk = true;
        foreach (var to in recipientList)
        {
            try
            {
                var ok = await SendEmailAsync(to, subject, body);
                if (!ok) allOk = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send low-stock email to {Recipient}", to);
                allOk = false;
            }
        }
        return allOk;
    }

    public async Task<bool> SendRequestActionAsync(
        string recipientEmail,
        string requestedByName,
        RequestType requestType,
        bool approved,
        string reviewerName,
        string? reviewNote,
        string? subjectName,
        DateTime decidedAtUtc)
    {
        const string systemNameEn = "SMA Retail System";
        const string systemNameAr = "نظام هيئة الشارقة للمتاحف للبيع بالتجزئة";

        if (string.IsNullOrWhiteSpace(recipientEmail))
            return false;

        var (typeEn, typeAr) = GetRequestTypeLabels(requestType);
        var actionEn = approved ? "approved" : "rejected";
        var actionAr = approved ? "الموافقة على" : "رفض";
        var actionTitleEn = approved ? "Request Approved" : "Request Rejected";
        var actionTitleAr = approved ? "تمت الموافقة على الطلب" : "تم رفض الطلب";
        var accentColor = approved ? "#059669" : "#b91c1c";

        var subject = $"{actionTitleAr}: {typeAr} | {actionTitleEn}: {typeEn} - {systemNameEn}";

        var safeRequestedBy = WebUtility.HtmlEncode(requestedByName ?? string.Empty);
        var safeReviewer = WebUtility.HtmlEncode(reviewerName ?? string.Empty);
        var safeTypeEn = WebUtility.HtmlEncode(typeEn);
        var safeTypeAr = WebUtility.HtmlEncode(typeAr);
        var safeSubject = string.IsNullOrWhiteSpace(subjectName) ? null : WebUtility.HtmlEncode(subjectName);
        var safeNote = string.IsNullOrWhiteSpace(reviewNote) ? null : WebUtility.HtmlEncode(reviewNote);
        var decidedAtLocal = decidedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        var subjectRowEn = safeSubject == null ? string.Empty : $@"
                            <tr><td style='padding:6px 8px;color:#6b7280'>Item</td><td style='padding:6px 8px;font-weight:600'>{safeSubject}</td></tr>";
        var subjectRowAr = safeSubject == null ? string.Empty : $@"
                            <tr><td style='padding:6px 8px;color:#6b7280'>العنصر</td><td style='padding:6px 8px;font-weight:600'>{safeSubject}</td></tr>";

        var noteBlockEn = safeNote == null ? string.Empty : $@"
                        <div style='margin-top:16px;padding:12px;background:#f9fafb;border-left:3px solid {accentColor};border-radius:4px'>
                            <div style='color:#6b7280;font-size:12px;margin-bottom:4px'>Reviewer note</div>
                            <div>{safeNote}</div>
                        </div>";
        var noteBlockAr = safeNote == null ? string.Empty : $@"
                        <div style='margin-top:16px;padding:12px;background:#f9fafb;border-right:3px solid {accentColor};border-radius:4px'>
                            <div style='color:#6b7280;font-size:12px;margin-bottom:4px'>ملاحظة المراجع</div>
                            <div>{safeNote}</div>
                        </div>";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 720px; margin: 0 auto; padding: 20px;'>

                    <!-- Arabic section -->
                    <div dir='rtl' style='text-align:right;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>{actionTitleAr}</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameAr}</p>
                        <p>مرحباً {safeRequestedBy}،</p>
                        <p>تم {actionAr} طلبك بواسطة <strong>{safeReviewer}</strong>.</p>
                        <table style='border-collapse:collapse;width:100%;margin-top:12px'>
                            <tbody>
                                <tr><td style='padding:6px 8px;color:#6b7280;width:160px'>نوع الطلب</td><td style='padding:6px 8px;font-weight:600'>{safeTypeAr}</td></tr>{subjectRowAr}
                                <tr><td style='padding:6px 8px;color:#6b7280'>القرار</td><td style='padding:6px 8px;font-weight:600;color:{accentColor}'>{actionTitleAr}</td></tr>
                                <tr><td style='padding:6px 8px;color:#6b7280'>تمت المراجعة في</td><td style='padding:6px 8px'>{decidedAtLocal}</td></tr>
                            </tbody>
                        </table>{noteBlockAr}
                        <p style='color:#666;margin-top:24px'>مع أطيب التحيات،<br/>فريق {systemNameAr}</p>
                    </div>

                    <hr style='margin:32px 0;border:none;border-top:1px solid #e5e7eb' />

                    <!-- English section -->
                    <div dir='ltr' style='text-align:left;'>
                        <h2 style='color: {accentColor}; margin-bottom: 4px;'>{actionTitleEn}</h2>
                        <p style='color:#6b7280;margin-top:0;font-size:13px'>{systemNameEn}</p>
                        <p>Hello {safeRequestedBy},</p>
                        <p>Your request has been {actionEn} by <strong>{safeReviewer}</strong>.</p>
                        <table style='border-collapse:collapse;width:100%;margin-top:12px'>
                            <tbody>
                                <tr><td style='padding:6px 8px;color:#6b7280;width:160px'>Request type</td><td style='padding:6px 8px;font-weight:600'>{safeTypeEn}</td></tr>{subjectRowEn}
                                <tr><td style='padding:6px 8px;color:#6b7280'>Decision</td><td style='padding:6px 8px;font-weight:600;color:{accentColor}'>{actionTitleEn}</td></tr>
                                <tr><td style='padding:6px 8px;color:#6b7280'>Reviewed at</td><td style='padding:6px 8px'>{decidedAtLocal}</td></tr>
                            </tbody>
                        </table>{noteBlockEn}
                        <p style='color:#666;margin-top:24px'>Best regards,<br/>{systemNameEn} Team</p>
                    </div>

                </div>
            </body>
            </html>
        ";

        try
        {
            return await SendEmailAsync(recipientEmail, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send request-action email to {Recipient}", recipientEmail);
            return false;
        }
    }

    private static (string En, string Ar) GetRequestTypeLabels(RequestType type) => type switch
    {
        RequestType.ChangePrice => ("Change Price", "تغيير السعر"),
        RequestType.SetUnitPrice => ("Set Unit Price", "تحديد سعر الوحدة"),
        RequestType.ActivateProduct => ("Activate Product", "تفعيل المنتج"),
        RequestType.ActivateUnit => ("Activate Unit", "تفعيل الوحدة"),
        RequestType.AddProduct => ("Add Product", "إضافة منتج"),
        RequestType.UpdateProduct => ("Update Product", "تحديث منتج"),
        RequestType.AddUnit => ("Add Unit", "إضافة وحدة"),
        RequestType.UpdateUnit => ("Update Unit", "تحديث وحدة"),
        RequestType.AddGRN => ("Add GRN", "إضافة إذن استلام"),
        RequestType.AddStockAdjustment => ("Add Stock Adjustment", "إضافة تسوية مخزون"),
        RequestType.AddStockTransfer => ("Add Stock Transfer", "إضافة نقل مخزون"),
        RequestType.DeleteProduct => ("Delete Product", "حذف منتج"),
        RequestType.DeleteUnit => ("Delete Unit", "حذف وحدة"),
        RequestType.SetLogisticsDetails => ("Set Logistics Details", "تحديد تفاصيل اللوجستيات"),
        _ => (type.ToString(), type.ToString())
    };

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
