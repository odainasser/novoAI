using Domain.Constants;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class PermissionSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PermissionSeeder> _logger;

    public PermissionSeeder(
        ApplicationDbContext context,
        ILogger<PermissionSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting permission seeding...");

        var permissions = new List<(string Code, string NameEn, string NameAr, string DescriptionEn, string DescriptionAr, string Module)>
        {
            // User Permissions
            (Permissions.UsersRead, "Read Users", "عرض المستخدمين", "View user information and list users", "عرض بيانات وقائمة المستخدمين", "Users"),
            (Permissions.UsersWrite, "Write Users", "إدارة المستخدمين", "Create and update user information", "إنشاء وتحديث بيانات المستخدمين", "Users"),
            (Permissions.UsersDelete, "Delete Users", "حذف المستخدمين", "Delete user accounts", "حذف حسابات المستخدمين", "Users"),
            // Role Permissions
            (Permissions.RolesRead, "Read Roles", "عرض الأدوار", "View role information and list roles", "عرض بيانات وقائمة الأدوار", "Roles"),
            (Permissions.RolesWrite, "Write Roles", "إدارة الأدوار", "Create and update roles", "إنشاء وتحديث الأدوار", "Roles"),
            (Permissions.RolesDelete, "Delete Roles", "حذف الأدوار", "Delete roles", "حذف الأدوار", "Roles"),
            // System Permissions
            (Permissions.SystemAudit, "System Audit", "سجلات النظام", "View system audit logs and reports", "عرض سجلات وتقارير النظام", "System"),
            // Lookup Permissions
            (Permissions.LookupsRead, "Read Lookups", "عرض القوائم", "View lookups information and list lookups", "عرض بيانات وقائمة القوائم", "Lookups"),
            (Permissions.LookupsWrite, "Write Lookups", "إدارة القوائم", "Create and update lookups", "إنشاء وتحديث القوائم", "Lookups"),
            (Permissions.LookupsDelete, "Delete Lookups", "حذف القوائم", "Delete lookups", "حذف القوائم", "Lookups"),
            // AI Assistant Keyword Permissions
            (Permissions.AssistantKeywordsRead, "Read Assistant Keywords", "عرض كلمات المساعد", "View assistant keyword dictionary, misses, and suggestions", "عرض قاموس كلمات المساعد والأسئلة غير المفهومة والاقتراحات", "Assistant"),
            (Permissions.AssistantKeywordsWrite, "Manage Assistant Keywords", "إدارة كلمات المساعد", "Create, edit, and delete assistant keyword triggers", "إنشاء وتعديل وحذف كلمات المساعد", "Assistant"),
            (Permissions.AssistantKeywordsApprove, "Approve Keyword Suggestions", "اعتماد اقتراحات الكلمات", "Approve or reject suggested assistant keywords", "اعتماد أو رفض الكلمات المقترحة للمساعد", "Assistant"),
            // Apps integration module
            (Permissions.AppsRead, "Read Apps", "عرض التطبيقات", "View registered client applications", "عرض التطبيقات المسجلة", "Apps"),
            (Permissions.AppsWrite, "Manage Apps", "إدارة التطبيقات", "Register, edit, activate, and deactivate client applications", "تسجيل وتعديل وتفعيل وتعطيل التطبيقات", "Apps")
        };

        foreach (var (code, nameEn, nameAr, descriptionEn, descriptionAr, module) in permissions)
        {
            var existingPermission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Code == code);

            if (existingPermission == null)
            {
                var permission = new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    NameEn = nameEn,
                    NameAr = nameAr,
                    DescriptionEn = descriptionEn,
                    DescriptionAr = descriptionAr,
                    Module = module,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Permissions.Add(permission);
                _logger.LogInformation("Permission '{Code}' created", code);
            }
            else
            {
                 // Update existing permission with new translations if needed?
                 // Usually seeders might want to update existing records if they are system defined.
                 // But for now, let's just stick to creation or maybe update fields if they are missing/different?
                 // The prompt "update current migrations and seeders" suggests we should ensure data is correct.
                 if (existingPermission.NameEn != nameEn || existingPermission.NameAr != nameAr)
                 {
                     existingPermission.NameEn = nameEn;
                     existingPermission.NameAr = nameAr;
                     existingPermission.DescriptionEn = descriptionEn;
                     existingPermission.DescriptionAr = descriptionAr;
                     _context.Permissions.Update(existingPermission);
                     _logger.LogInformation("Permission '{Code}' updated", code);
                 }
                 else
                 {
                    _logger.LogInformation("Permission '{Code}' already exists", code);
                 }
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Permission seeding completed");
    }
}
