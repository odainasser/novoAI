using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class SupplierSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SupplierSeeder> _logger;

    public SupplierSeeder(ApplicationDbContext context, ILogger<SupplierSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting supplier seeding...");

        var suppliers = new List<Supplier>
        {
            new() { NameEn = "Fresh Foods Trading Co.",       NameAr = "شركة الأغذية الطازجة للتجارة",     ContactPersonEn = "Ahmed Al Jamal",     ContactPersonAr = "أحمد الجمل",       ContactEmail = "orders@freshfoods.ae",        ContactPhone = "+971-6-555-0101", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { NameEn = "Gulf Dairy & Beverages",        NameAr = "ألبان ومشروبات الخليج",            ContactPersonEn = "Fatima Khalid",      ContactPersonAr = "فاطمة خالد",       ContactEmail = "sales@gulfdairy.ae",          ContactPhone = "+971-6-555-0102", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { NameEn = "National Bakery Supplies",      NameAr = "الإمدادات الوطنية للمخابز",        ContactPersonEn = "Mohammed Rashid",    ContactPersonAr = "محمد راشد",        ContactEmail = "orders@nationalbakery.ae",    ContactPhone = "+971-6-555-0103", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { NameEn = "Snack World Distribution",      NameAr = "عالم الوجبات الخفيفة للتوزيع",      ContactPersonEn = "Sara Al Mazrouei",   ContactPersonAr = "سارة المزروعي",    ContactEmail = "info@snackworld.ae",          ContactPhone = "+971-6-555-0104", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { NameEn = "Mediterranean Imports LLC",     NameAr = "الواردات المتوسطية ذ.م.م",          ContactPersonEn = "Khalid Bin Saeed",   ContactPersonAr = "خالد بن سعيد",     ContactEmail = "supply@medimports.ae",        ContactPhone = "+971-6-555-0105", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { NameEn = "Home Essentials Trading",       NameAr = "تجارة لوازم المنزل",                ContactPersonEn = "Noura Hassan",       ContactPersonAr = "نورة حسن",         ContactEmail = "contact@homeessentials.ae",   ContactPhone = "+971-6-555-0106", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { NameEn = "Personal Care Distributors",    NameAr = "موزعو منتجات العناية الشخصية",      ContactPersonEn = "Omar Al Suwaidi",    ContactPersonAr = "عمر السويدي",      ContactEmail = "orders@personalcare.ae",      ContactPhone = "+971-6-555-0107", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { NameEn = "Farm Fresh Produce",            NameAr = "محاصيل المزرعة الطازجة",            ContactPersonEn = "Hassan Al Marri",    ContactPersonAr = "حسن المري",        ContactEmail = "sales@farmfresh.ae",          ContactPhone = "+971-6-555-0108", IsActive = true, CreatedAt = DateTime.UtcNow },
        };

        var existingNames = await _context.Suppliers
            .Select(s => s.NameEn)
            .ToListAsync();

        var newSuppliers = suppliers.Where(s => !existingNames.Contains(s.NameEn)).ToList();

        if (newSuppliers.Count == 0)
        {
            _logger.LogInformation("All suppliers already exist. Skipping.");
            return;
        }

        await _context.Suppliers.AddRangeAsync(newSuppliers);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} suppliers.", newSuppliers.Count);
    }
}
