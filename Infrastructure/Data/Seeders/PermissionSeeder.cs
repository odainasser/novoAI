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
            // Category Permissions
            (Permissions.CategoriesRead, "Read Categories", "عرض الفئات", "View categories information and list categories", "عرض بيانات وقائمة الفئات", "Categories"),
            (Permissions.CategoriesWrite, "Write Categories", "إدارة الفئات", "Create and update categories", "إنشاء وتحديث الفئات", "Categories"),
            (Permissions.CategoriesDelete, "Delete Categories", "حذف الفئات", "Delete categories", "حذف الفئات", "Categories"),
            // Product Permissions
            (Permissions.ProductsRead, "Read Products", "عرض المنتجات", "View products information and list products", "عرض بيانات وقائمة المنتجات", "Products"),
            (Permissions.ProductsWrite, "Write Products", "إدارة المنتجات", "Create and update products", "إنشاء وتحديث المنتجات", "Products"),
            (Permissions.ProductsDelete, "Delete Products", "حذف المنتجات", "Delete products", "حذف المنتجات", "Products"),
            // Supplier Permissions
            (Permissions.SuppliersRead, "Read Suppliers", "عرض الموردين", "View suppliers information and list suppliers", "عرض بيانات وقائمة الموردين", "Suppliers"),
            (Permissions.SuppliersWrite, "Write Suppliers", "إدارة الموردين", "Create and update suppliers", "إنشاء وتحديث الموردين", "Suppliers"),
            (Permissions.SuppliersDelete, "Delete Suppliers", "حذف الموردين", "Delete suppliers", "حذف الموردين", "Suppliers"),
            // Request Permissions
            (Permissions.RequestsRead, "Read Requests", "عرض الطلبات", "View all requests in the system", "عرض جميع الطلبات في النظام", "Requests"),
            (Permissions.RequestsWrite, "Manage Requests", "إدارة الطلبات", "Create and review requests", "إنشاء ومراجعة الطلبات", "Requests"),
            // Branch Permissions
            (Permissions.BranchesRead, "Read Branches", "عرض المتاحف", "View branches information and list branches", "عرض بيانات وقائمة المتاحف", "Branches"),
            (Permissions.BranchesWrite, "Write Branches", "إدارة المتاحف", "Create and update branches", "إنشاء وتحديث المتاحف", "Branches"),
            (Permissions.BranchesDelete, "Delete Branches", "حذف المتاحف", "Delete branches", "حذف المتاحف", "Branches"),
            // Warehouse Permissions
            (Permissions.WarehousesRead, "Read Warehouses", "عرض المستودعات", "View warehouses information and list warehouses", "عرض بيانات وقائمة المستودعات", "Warehouses"),
            (Permissions.WarehousesWrite, "Write Warehouses", "إدارة المستودعات", "Create and update warehouses", "إنشاء وتحديث المستودعات", "Warehouses"),
            (Permissions.WarehousesDelete, "Delete Warehouses", "حذف المستودعات", "Delete warehouses", "حذف المستودعات", "Warehouses"),
            // Terminal Permissions
            (Permissions.TerminalsRead, "Read Terminals", "عرض الطرفيات", "View terminals information and list terminals", "عرض بيانات وقائمة الطرفيات", "Terminals"),
            (Permissions.TerminalsWrite, "Write Terminals", "إدارة الطرفيات", "Create and update terminals", "إنشاء وتحديث الطرفيات", "Terminals"),
            (Permissions.TerminalsDelete, "Delete Terminals", "حذف الطرفيات", "Delete terminals", "حذف الطرفيات", "Terminals"),
            // Inventory Permissions
            (Permissions.InventoryRead, "Read Inventory", "عرض المخزون", "View inventory operations and stock balances", "عرض عمليات المخزون وأرصدة المخزون", "Inventory"),
            (Permissions.InventoryWrite, "Write Inventory", "إدارة المخزون", "Create and manage inventory operations", "إنشاء وإدارة عمليات المخزون", "Inventory"),
            (Permissions.InventoryApprove, "Approve Inventory", "اعتماد المخزون", "Approve or reject inventory operations", "اعتماد أو رفض عمليات المخزون", "Inventory"),
            (Permissions.InventoryDelete, "Delete Inventory", "حذف المخزون", "Delete inventory operations", "حذف عمليات المخزون", "Inventory"),
            // Purchase Request Permissions
            (Permissions.PurchaseRequestsRead, "Read Purchase Requests", "عرض طلبات الشراء", "View purchase requests and their lines", "عرض طلبات الشراء وبنودها", "Inventory"),
            (Permissions.PurchaseRequestsWrite, "Write Purchase Requests", "إدارة طلبات الشراء", "Create, edit, submit, and cancel purchase requests", "إنشاء وتعديل وإرسال وإلغاء طلبات الشراء", "Inventory"),
            (Permissions.PurchaseRequestsApprove, "Approve Purchase Requests", "اعتماد طلبات الشراء", "Approve or reject purchase requests", "اعتماد أو رفض طلبات الشراء", "Inventory"),
            (Permissions.PurchaseRequestsConvert, "Convert Purchase Requests", "تحويل طلبات الشراء", "Convert approved purchase requests to GRN or stock transfer", "تحويل طلبات الشراء المعتمدة إلى إذن استلام أو نقل مخزون", "Inventory"),
            // Unit Permissions
            (Permissions.UnitsRead, "Read Units", "عرض الوحدات", "View units information and list units", "عرض بيانات وقائمة الوحدات", "Units"),
            (Permissions.UnitsWrite, "Write Units", "إدارة الوحدات", "Create and update units", "إنشاء وتحديث الوحدات", "Units"),
            (Permissions.UnitsDelete, "Delete Units", "حذف الوحدات", "Delete units", "حذف الوحدات", "Units"),
            (Permissions.UnitsPrice, "Set Selling Details", "تحديد تفاصيل البيع", "Set selling price and barcode for units", "تحديد سعر البيع والباركود للوحدات", "Units"),
            (Permissions.UnitsLogistics, "Set Logistics Details", "تحديد تفاصيل اللوجستيات", "Set cost and supplier barcode for units", "تحديد سعر التكلفة وباركود المورد للوحدات", "Units"),
            // AI Assistant Keyword Permissions
            (Permissions.AssistantKeywordsRead, "Read Assistant Keywords", "عرض كلمات المساعد", "View assistant keyword dictionary, misses, and suggestions", "عرض قاموس كلمات المساعد والأسئلة غير المفهومة والاقتراحات", "Assistant"),
            (Permissions.AssistantKeywordsWrite, "Manage Assistant Keywords", "إدارة كلمات المساعد", "Create, edit, and delete assistant keyword triggers", "إنشاء وتعديل وحذف كلمات المساعد", "Assistant"),
            (Permissions.AssistantKeywordsApprove, "Approve Keyword Suggestions", "اعتماد اقتراحات الكلمات", "Approve or reject suggested assistant keywords", "اعتماد أو رفض الكلمات المقترحة للمساعد", "Assistant")
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
