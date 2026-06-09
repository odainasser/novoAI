using Domain.Entities;
using Domain.Common;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Application.Common.Interfaces;

namespace Infrastructure.Persistence;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ICurrentUserService? _currentUserService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<User> DomainUsers { get; set; }
    public DbSet<Role> DomainRoles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public new DbSet<UserRole> UserRoles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<Lookup> Lookups { get; set; }
    public DbSet<UserLog> UserLogs { get; set; }
    public DbSet<Media> Media { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OrderRefund> OrderRefunds { get; set; }
    public DbSet<OrderRefundItem> OrderRefundItems { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<PromotionUnit> PromotionUnits { get; set; }
    public DbSet<PromotionCategory> PromotionCategories { get; set; }
    public DbSet<Domain.Entities.Shift> Shifts { get; set; }
    public DbSet<Domain.Entities.Request> Requests { get; set; }
    public DbSet<Domain.Entities.Branch> Branches { get; set; }
    public DbSet<Domain.Entities.Warehouse> Warehouses { get; set; }
    public DbSet<Domain.Entities.Terminal> Terminals { get; set; }
    public DbSet<Domain.Entities.CashierWarehouse> CashierWarehouses { get; set; }
    public DbSet<Domain.Entities.UserBranch> UserBranches { get; set; }

    public DbSet<Unit> Units { get; set; }
    public DbSet<UnitSupplier> UnitSuppliers { get; set; }

    // Inventory Management
    public DbSet<StockBalance> StockBalances { get; set; }
    public DbSet<GoodsReceivingNote> GoodsReceivingNotes { get; set; }
    public DbSet<GoodsReceivingNoteLine> GoodsReceivingNoteLines { get; set; }
    public DbSet<StockAdjustment> StockAdjustments { get; set; }
    public DbSet<StockAdjustmentLine> StockAdjustmentLines { get; set; }
    public DbSet<InventoryHistory> InventoryHistories { get; set; }
    public DbSet<StockTransfer> StockTransfers { get; set; }
    public DbSet<StockTransferLine> StockTransferLines { get; set; }
    public DbSet<Stocktake> Stocktakes { get; set; }
    public DbSet<StocktakeLine> StocktakeLines { get; set; }
    public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
    public DbSet<PurchaseRequestLine> PurchaseRequestLines { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // AI assistant (tool-calling) — turn log + governed plan library + no-answer queue
    public DbSet<AssistantInteraction> AssistantInteractions { get; set; }
    public DbSet<AssistantPlan> AssistantPlans { get; set; }
    public DbSet<AssistantNoAnswer> AssistantNoAnswers { get; set; }
    public DbSet<AssistantReportedAnswer> AssistantReportedAnswers { get; set; }

    // Atomic document-number counters (orders, goods-receiving notes, …)
    public DbSet<NumberSequence> NumberSequences { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Atomic document-number sequences ──────────────────────────
        builder.Entity<NumberSequence>(entity =>
        {
            entity.ToTable("NumberSequences");
            entity.HasKey(s => s.Key);
            entity.Property(s => s.Key).HasMaxLength(64);
            entity.Property(s => s.Value).IsConcurrencyToken(false);
        });

        // ── AI assistant (tool-calling): turn log shown as reviewable plans ──
        builder.Entity<AssistantInteraction>(entity =>
        {
            entity.Property(i => i.Question).IsRequired().HasMaxLength(2000);
            entity.Property(i => i.Locale).HasMaxLength(10);
            entity.Property(i => i.ToolsUsed).HasMaxLength(500);
            entity.Property(i => i.Answer).HasMaxLength(2000);
            entity.Property(i => i.ConfirmedTools).HasMaxLength(500);
            entity.Property(i => i.ConfirmedDomain).HasMaxLength(100);
            entity.Property(i => i.ConfirmedEntities).HasMaxLength(500);
            entity.Property(i => i.ReviewedBy).HasMaxLength(256);
            entity.HasIndex(i => i.CreatedAt);
            entity.HasIndex(i => i.PlanConfirmed);
        });

        builder.Entity<AssistantPlan>(entity =>
        {
            entity.Property(p => p.MatchDomains).IsRequired().HasMaxLength(120);
            entity.Property(p => p.Action).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Entity).IsRequired().HasMaxLength(60);
            entity.Property(p => p.SecondaryEntity).HasMaxLength(60);
            entity.Property(p => p.MatchKey).IsRequired().HasMaxLength(256);
            entity.Property(p => p.SampleQuestion).HasMaxLength(2000);
            entity.Property(p => p.Locale).HasMaxLength(10);
            entity.Property(p => p.ConfirmedBy).HasMaxLength(256);
            entity.HasIndex(p => new { p.MatchKey, p.Status });
            entity.HasQueryFilter(p => !p.IsDeleted);
        });

        builder.Entity<AssistantNoAnswer>(entity =>
        {
            entity.Property(c => c.NormalizedQuestion).IsRequired().HasMaxLength(400);
            entity.Property(c => c.ClusterKey).IsRequired().HasMaxLength(420);
            entity.Property(c => c.SampleQuestion).IsRequired().HasMaxLength(2000);
            entity.Property(c => c.Locale).HasMaxLength(10);
            entity.Property(c => c.Evidence).HasMaxLength(2000);
            entity.Property(c => c.UserFacingMessage).HasMaxLength(1000);
            entity.Property(c => c.ReviewedBy).HasMaxLength(256);
            entity.HasIndex(c => c.ClusterKey).IsUnique();
            entity.HasIndex(c => c.Frequency);
            entity.HasQueryFilter(c => !c.IsDeleted);
        });

        builder.Entity<AssistantReportedAnswer>(entity =>
        {
            entity.Property(r => r.Question).IsRequired().HasMaxLength(2000);
            entity.Property(r => r.Answer).IsRequired().HasMaxLength(8000);
            entity.Property(r => r.Feedback).HasMaxLength(2000);
            entity.Property(r => r.Locale).HasMaxLength(10);
            entity.Property(r => r.ReportedBy).HasMaxLength(256);
            entity.Property(r => r.ReviewedBy).HasMaxLength(256);
            entity.HasIndex(r => r.CreatedAt);
            entity.HasIndex(r => r.Resolved);
            entity.HasQueryFilter(r => !r.IsDeleted);
        });

        // Configure ApplicationRole
        builder.Entity<ApplicationRole>(entity =>
        {
            entity.Property(r => r.DescriptionEn).HasMaxLength(500);
            entity.Property(r => r.DescriptionAr).HasMaxLength(500);
            entity.Property(r => r.IsSystemRole).IsRequired();
            entity.HasQueryFilter(r => !r.IsDeleted);
        });

        // Configure ApplicationUser query filter
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasQueryFilter(u => !u.IsDeleted);
        });

        // Configure Supplier entity
        builder.Entity<Supplier>(entity =>
        {
            entity.HasQueryFilter(s => !s.IsDeleted);
        });

        // Configure OrderRefund entity
        builder.Entity<OrderRefund>(entity =>
        {
            entity.ToTable("OrderRefunds");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Amount).HasColumnType("decimal(18,2)");
            entity.Property(r => r.CreatedAt).IsRequired();
            entity.Property(r => r.Reason).HasMaxLength(500);

            entity.HasOne(r => r.Order)
                .WithMany(o => o.Refunds)
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(r => !r.IsDeleted);
        });

        builder.Entity<OrderRefundItem>(entity =>
        {
            entity.ToTable("OrderRefundItems");
            entity.HasKey(ri => ri.Id);
            entity.Property(ri => ri.Quantity).IsRequired();
            entity.Property(ri => ri.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(ri => ri.Total).HasColumnType("decimal(18,2)");

            entity.HasOne(ri => ri.OrderRefund)
                .WithMany(r => r.Items)
                .HasForeignKey(ri => ri.OrderRefundId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(ri => !ri.IsDeleted);
        });

        // Configure Lookup entity
        builder.Entity<Lookup>(entity =>
        {
            entity.ToTable("Lookups");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Code).IsRequired().HasMaxLength(50);
            entity.Property(l => l.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(l => l.NameAr).IsRequired().HasMaxLength(200).IsUnicode(true);

            entity.HasOne(l => l.Parent)
                .WithMany(l => l.Children)
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(l => !l.IsDeleted);
        });

        // Configure Domain User entity
        builder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);

            entity.HasQueryFilter(u => !u.IsDeleted);
        });

        // Configure Domain Role entity
        builder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(256);
            entity.HasIndex(r => r.Name).IsUnique();
            entity.HasQueryFilter(r => !r.IsDeleted);
        });

        // Configure Permission entity
        builder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(100);
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.NameEn).IsRequired().HasMaxLength(256);
            entity.Property(p => p.NameAr).IsRequired().HasMaxLength(256);
            entity.Property(p => p.DescriptionEn).HasMaxLength(500);
            entity.Property(p => p.DescriptionAr).HasMaxLength(500);
        });

        // Configure UserRole many-to-many
        builder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Match query filters on User and Role to avoid unexpected results
            // when a required navigation end is filtered out by a global query filter.
            entity.HasQueryFilter(ur => !ur.Role.IsDeleted && !ur.User.IsDeleted);
        });

        // Configure RolePermission many-to-many
        builder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            entity.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure UserLog entity
        builder.Entity<UserLog>(entity =>
        {
            entity.ToTable("UserLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(50)
                .HasConversion<string>();
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Timestamp);
        });

        // Configure Notification entity
        builder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Type)
                .IsRequired()
                .HasConversion<int>();
            entity.Property(n => n.TitleEn).IsRequired().HasMaxLength(200);
            entity.Property(n => n.TitleAr).IsRequired().HasMaxLength(200);
            entity.Property(n => n.BodyEn).IsRequired().HasMaxLength(1000);
            entity.Property(n => n.BodyAr).IsRequired().HasMaxLength(1000);
            entity.Property(n => n.Link).HasMaxLength(500);
            entity.HasIndex(n => new { n.UserId, n.IsRead });
            entity.HasIndex(n => n.CreatedAt);
        });

        // Configure Media entity
        builder.Entity<Media>(entity =>
        {
            entity.ToTable("Media");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(m => m.EntityId).IsRequired();
            entity.Property(m => m.CollectionName).IsRequired().HasMaxLength(100);
            entity.Property(m => m.Name).IsRequired().HasMaxLength(255);
            entity.Property(m => m.FileName).IsRequired().HasMaxLength(255);
            entity.Property(m => m.MimeType).HasMaxLength(100);
            entity.Property(m => m.Disk).HasMaxLength(50);
            entity.Property(m => m.Path).IsRequired().HasMaxLength(500);
            
            entity.HasIndex(m => new { m.EntityType, m.EntityId });
            entity.HasQueryFilter(m => !m.IsDeleted);
        });

        // Configure Category entity
        builder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(c => c.NameAr).IsRequired().HasMaxLength(200).IsUnicode(true);
            entity.Property(c => c.DescriptionEn).HasMaxLength(1000);
            entity.Property(c => c.DescriptionAr).HasMaxLength(1000).IsUnicode(true);

            entity.HasOne(c => c.Parent)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(c => !c.IsDeleted);

            entity.HasIndex(c => c.NameEn);
            entity.HasIndex(c => c.NameAr);
            entity.HasIndex(c => c.ParentId);
            entity.HasIndex(c => c.SortOrder);
        });

        // Configure Product entity
        builder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(p => p.NameAr).IsRequired().HasMaxLength(200).IsUnicode(true);
            entity.Property(p => p.DescriptionEn).HasMaxLength(1000);
            entity.Property(p => p.DescriptionAr).HasMaxLength(1000).IsUnicode(true);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(100);

            entity.HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(p => !p.IsDeleted);

            entity.HasIndex(p => p.Code).IsUnique();
            entity.HasIndex(p => p.NameEn);
            entity.HasIndex(p => p.NameAr);
            entity.HasIndex(p => p.CategoryId);
            entity.HasIndex(p => p.IsActive);
        });

        // Configure Order entity
        builder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(o => o.IdempotencyKey).HasMaxLength(100);
            entity.Property(o => o.Channel).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(o => o.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(o => o.PaymentMethod).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(o => o.CashAmount).HasColumnType("decimal(18,2)");
            entity.Property(o => o.CardAmount).HasColumnType("decimal(18,2)");
            entity.Property(o => o.Subtotal).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(o => o.VatRate).IsRequired().HasColumnType("decimal(5,4)");
            entity.Property(o => o.VatAmount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(o => o.Total).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(o => o.CashierName).HasMaxLength(200);
            entity.Property(o => o.CustomerName).HasMaxLength(200);
            entity.Property(o => o.CustomerEmail).HasMaxLength(256);
            entity.Property(o => o.CustomerPhone).HasMaxLength(50);
            entity.Property(o => o.Notes).HasMaxLength(1000);
            entity.Property(o => o.CancellationReason).HasMaxLength(500);

            entity.HasQueryFilter(o => !o.IsDeleted);

            entity.HasIndex(o => o.OrderNumber).IsUnique();
            // Unique only among rows that actually carry a key (filtered index)
            // so the duplicate-suppression covers offline replays without
            // forcing every order to have a key.
            entity.HasIndex(o => o.IdempotencyKey)
                  .IsUnique()
                  .HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(o => o.CashierId);
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.Channel);
            entity.HasIndex(o => o.CreatedAt);
        });

        // Configure OrderItem entity
        builder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(oi => oi.Id);
            entity.Property(oi => oi.ProductNameEn).IsRequired().HasMaxLength(200);
            entity.Property(oi => oi.ProductNameAr).IsRequired().HasMaxLength(200).IsUnicode(true);
            entity.Property(oi => oi.ProductCode).IsRequired().HasMaxLength(100);
            entity.Property(oi => oi.UnitNameEn).HasMaxLength(200).HasColumnName("SellingUnitNameEn");
            entity.Property(oi => oi.UnitNameAr).HasMaxLength(200).IsUnicode(true).HasColumnName("SellingUnitNameAr");
            entity.Property(oi => oi.UnitBarcode).HasMaxLength(48).HasColumnName("SellingUnitBarcode");
            entity.Property(oi => oi.Quantity).IsRequired();
            entity.Property(oi => oi.UnitPrice).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(oi => oi.Total).IsRequired().HasColumnType("decimal(18,2)");

            entity.HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(oi => oi.Unit)
                .WithMany()
                .HasForeignKey(oi => oi.UnitId)
                .HasConstraintName("FK_OrderItems_SellingUnits_SellingUnitId")
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(oi => oi.UnitId).HasColumnName("SellingUnitId");

            entity.HasIndex(oi => oi.OrderId);
            entity.HasIndex(oi => oi.ProductId);

            entity.HasQueryFilter(oi => !oi.IsDeleted);
        });

        // Configure Promotion entity
        builder.Entity<Promotion>(entity =>
        {
            entity.ToTable("Promotions");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(p => p.NameAr).IsRequired().HasMaxLength(200).IsUnicode(true);
            entity.Property(p => p.DescriptionEn).HasMaxLength(1000);
            entity.Property(p => p.DescriptionAr).HasMaxLength(1000).IsUnicode(true);
            entity.Property(p => p.DiscountType).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(p => p.DiscountValue).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(p => p.ApplyTo).IsRequired().HasConversion<int>();

            entity.HasQueryFilter(p => !p.IsDeleted);

            entity.HasIndex(p => p.NameEn);
            entity.HasIndex(p => p.NameAr);
            entity.HasIndex(p => p.IsActive);
            entity.HasIndex(p => new { p.StartDateTime, p.EndDateTime });
        });

        // Configure PromotionUnit entity
        builder.Entity<PromotionUnit>(entity =>
        {
            entity.ToTable("PromotionUnits");
            entity.HasKey(pu => pu.Id);

            entity.HasOne(pu => pu.Promotion)
                .WithMany(p => p.PromotionUnits)
                .HasForeignKey(pu => pu.PromotionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pu => pu.Unit)
                .WithMany()
                .HasForeignKey(pu => pu.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(pu => new { pu.PromotionId, pu.UnitId }).IsUnique();

            entity.HasQueryFilter(pu => !pu.IsDeleted);
        });

        // Configure PromotionCategory entity
        builder.Entity<PromotionCategory>(entity =>
        {
            entity.ToTable("PromotionCategories");
            entity.HasKey(pc => pc.Id);

            entity.HasOne(pc => pc.Promotion)
                .WithMany(p => p.PromotionCategories)
                .HasForeignKey(pc => pc.PromotionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pc => pc.Category)
                .WithMany()
                .HasForeignKey(pc => pc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(pc => new { pc.PromotionId, pc.CategoryId }).IsUnique();

            entity.HasQueryFilter(pc => !pc.IsDeleted);
        });

        // Configure Request entity
        builder.Entity<Domain.Entities.Request>(entity =>
        {
            entity.ToTable("Requests");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Type).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(r => r.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(r => r.RequestedById).IsRequired();
            entity.Property(r => r.RequestedByName).HasMaxLength(200);
            entity.Property(r => r.ProductName).HasMaxLength(200);
            entity.Property(r => r.CurrentPrice).HasColumnType("decimal(18,2)");
            entity.Property(r => r.NewPrice).HasColumnType("decimal(18,2)");
            entity.Property(r => r.Note).HasMaxLength(500);
            entity.Property(r => r.ApprovedByName).HasMaxLength(200);
            entity.Property(r => r.RejectedByName).HasMaxLength(200);
            entity.Property(r => r.ReviewNote).HasMaxLength(500);

            entity.HasQueryFilter(r => !r.IsDeleted);

            entity.HasIndex(r => r.Type);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.RequestedById);
            entity.HasIndex(r => r.UnitId).HasDatabaseName("IX_Requests_SellingUnitId");

            entity.Property(r => r.UnitId).HasColumnName("SellingUnitId");
        });

        // Configure Branch entity
        builder.Entity<Domain.Entities.Branch>(entity =>
        {
            entity.ToTable("Branches");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(m => m.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(m => m.DescriptionEn).HasMaxLength(1000);
            entity.Property(m => m.DescriptionAr).HasMaxLength(1000);

            entity.HasQueryFilter(m => !m.IsDeleted);
        });

        // Configure Warehouse entity
        builder.Entity<Domain.Entities.Warehouse>(entity =>
        {
            entity.ToTable("Warehouses");
            entity.HasKey(w => w.Id);
            entity.Property(w => w.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(w => w.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(w => w.Address).HasMaxLength(500);
            entity.Property(w => w.ContactPerson).HasMaxLength(200);
            entity.Property(w => w.ContactPhone).HasMaxLength(50);
            entity.Property(w => w.Email).HasMaxLength(256);

            entity.HasOne(w => w.WarehouseType)
                .WithMany()
                .HasForeignKey(w => w.WarehouseTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(w => w.Branch)
                .WithMany()
                .HasForeignKey(w => w.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(w => !w.IsDeleted);

            entity.HasIndex(w => w.NameEn);
            entity.HasIndex(w => w.NameAr);
            entity.HasIndex(w => w.WarehouseTypeId);
            entity.HasIndex(w => w.BranchId);
        });

        // Configure Terminal entity
        builder.Entity<Domain.Entities.Terminal>(entity =>
        {
            entity.ToTable("Devices");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(d => d.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(d => d.ComputerIp).HasMaxLength(45);
            entity.Property(d => d.PrinterIp).HasMaxLength(45);
            entity.Property(d => d.PaymentMachineIp).HasMaxLength(45);

            entity.HasOne(d => d.Branch)
                .WithMany()
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(d => !d.IsDeleted);

            entity.HasIndex(d => d.NameEn);
            entity.HasIndex(d => d.NameAr);
            entity.HasIndex(d => d.BranchId);
        });

        // Configure Shift entity
        builder.Entity<Domain.Entities.Shift>(entity =>
        {
            entity.ToTable("Shifts");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.StartTime).IsRequired();
            entity.Property(s => s.EndTime);
            entity.Property(s => s.TotalSales).HasColumnType("decimal(18,2)");
            entity.Property(s => s.TotalReturns).HasColumnType("decimal(18,2)");
            entity.Property(s => s.CashIn).HasColumnType("decimal(18,2)");
            entity.Property(s => s.CashOut).HasColumnType("decimal(18,2)");
            // Shift entity mapping
            entity.Property(s => s.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasConversion<string>();
            entity.Property(s => s.Comments).HasMaxLength(1000);
            entity.Property(s => s.WarehouseNameEn).HasMaxLength(200);
            entity.Property(s => s.WarehouseNameAr).HasMaxLength(200);

            entity.HasIndex(s => s.CashierId);
            entity.HasIndex(s => s.Status);
            entity.HasQueryFilter(s => !s.IsDeleted);
        });

        // Configure CashierWarehouse junction table
        builder.Entity<Domain.Entities.CashierWarehouse>(entity =>
        {
            entity.ToTable("CashierWarehouses");
            entity.HasKey(cw => new { cw.CashierId, cw.WarehouseId });

            entity.HasIndex(cw => cw.CashierId);
            entity.HasIndex(cw => cw.WarehouseId);
        });

        // Configure UserBranch junction table (branch-employee assignments)
        builder.Entity<Domain.Entities.UserBranch>(entity =>
        {
            entity.ToTable("UserBranches");
            entity.HasKey(ub => new { ub.UserId, ub.BranchId });

            entity.HasIndex(ub => ub.UserId);
            entity.HasIndex(ub => ub.BranchId);
        });

        // ===== Inventory Management Entities =====

        // Configure StockBalance entity
        builder.Entity<StockBalance>(entity =>
        {
            entity.ToTable("StockBalances");
            entity.HasKey(sb => sb.Id);
            entity.Property(sb => sb.AvailableQuantity).IsRequired();
            entity.Property(sb => sb.ReservedQuantity).IsRequired();
            entity.Property(sb => sb.InTransitQuantity).IsRequired();
            entity.Property(sb => sb.RowVersion).IsRowVersion();

            entity.HasOne(sb => sb.Warehouse)
                .WithMany()
                .HasForeignKey(sb => sb.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sb => sb.Unit)
                .WithMany()
                .HasForeignKey(sb => sb.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(sb => !sb.IsDeleted);

            entity.HasIndex(sb => new { sb.WarehouseId, sb.UnitId }).IsUnique();
            entity.HasIndex(sb => sb.WarehouseId);
            entity.HasIndex(sb => sb.UnitId);
        });

        // Configure GoodsReceivingNote entity
        builder.Entity<GoodsReceivingNote>(entity =>
        {
            entity.ToTable("GoodsReceivingNotes");
            entity.HasKey(g => g.Id);
            entity.Property(g => g.GRNNumber).IsRequired().HasMaxLength(50);
            entity.Property(g => g.PurchaseOrderReference).HasMaxLength(100);
            entity.Property(g => g.ReceivedBy).HasMaxLength(200);
            entity.Property(g => g.Notes).HasMaxLength(2000);
            entity.Property(g => g.AttachmentPath).HasMaxLength(500);

            entity.HasOne(g => g.Supplier)
                .WithMany()
                .HasForeignKey(g => g.SupplierId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(g => g.Warehouse)
                .WithMany()
                .HasForeignKey(g => g.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(g => !g.IsDeleted);

            entity.HasIndex(g => g.GRNNumber).IsUnique();
            entity.HasIndex(g => g.SupplierId);
            entity.HasIndex(g => g.WarehouseId);
            entity.HasIndex(g => g.ReceivedDate);
            entity.HasIndex(g => g.PurchaseRequestId);
        });

        // Configure GoodsReceivingNoteLine entity
        builder.Entity<GoodsReceivingNoteLine>(entity =>
        {
            entity.ToTable("GoodsReceivingNoteLines");
            entity.HasKey(gl => gl.Id);
            entity.Property(gl => gl.ReceivedQuantity).IsRequired();
            entity.Property(gl => gl.Notes).HasMaxLength(1000);

            entity.HasOne(gl => gl.GoodsReceivingNote)
                .WithMany(g => g.Lines)
                .HasForeignKey(gl => gl.GoodsReceivingNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(gl => gl.Unit)
                .WithMany()
                .HasForeignKey(gl => gl.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(gl => gl.Supplier)
                .WithMany()
                .HasForeignKey(gl => gl.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(gl => gl.Cost).HasColumnType("decimal(18,2)");

            entity.HasIndex(gl => gl.GoodsReceivingNoteId);
            entity.HasIndex(gl => gl.UnitId);

            entity.HasQueryFilter(gl => !gl.IsDeleted);
        });

        // Configure StockAdjustment entity
        builder.Entity<StockAdjustment>(entity =>
        {
            entity.ToTable("StockAdjustments");
            entity.HasKey(sa => sa.Id);
            entity.Property(sa => sa.AdjustmentNumber).IsRequired().HasMaxLength(50);
            entity.Property(sa => sa.AdjustmentType).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(sa => sa.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(sa => sa.RequestedByName).HasMaxLength(200);
            entity.Property(sa => sa.Explanation).HasMaxLength(2000);
            entity.Property(sa => sa.StocktakeNumber).HasMaxLength(50);

            entity.HasOne(sa => sa.Warehouse)
                .WithMany()
                .HasForeignKey(sa => sa.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(sa => !sa.IsDeleted);

            entity.HasIndex(sa => sa.AdjustmentNumber).IsUnique();
            entity.HasIndex(sa => sa.WarehouseId);
            entity.HasIndex(sa => sa.Status);
            entity.HasIndex(sa => sa.AdjustmentType);
            entity.HasIndex(sa => sa.RequestedDate);
            entity.HasIndex(sa => sa.StocktakeId);
        });

        // Configure StockAdjustmentLine entity
        builder.Entity<StockAdjustmentLine>(entity =>
        {
            entity.ToTable("StockAdjustmentLines");
            entity.HasKey(sal => sal.Id);
            entity.Property(sal => sal.CurrentQuantity).IsRequired();
            entity.Property(sal => sal.AdjustmentQuantity).IsRequired();
            entity.Property(sal => sal.NewQuantity).IsRequired();
            entity.Property(sal => sal.Notes).HasMaxLength(1000);

            entity.HasOne(sal => sal.StockAdjustment)
                .WithMany(sa => sa.Lines)
                .HasForeignKey(sal => sal.StockAdjustmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sal => sal.Unit)
                .WithMany()
                .HasForeignKey(sal => sal.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(sal => sal.StockAdjustmentId);
            entity.HasIndex(sal => sal.UnitId);

            entity.HasQueryFilter(sal => !sal.IsDeleted);
        });

        // Configure InventoryHistory entity (append-only, no soft delete)
        builder.Entity<InventoryHistory>(entity =>
        {
            entity.ToTable("InventoryHistories");
            entity.HasKey(ih => ih.Id);
            entity.Property(ih => ih.ActionType).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(ih => ih.QuantityChange).IsRequired();
            entity.Property(ih => ih.AvailableQuantityBefore).IsRequired();
            entity.Property(ih => ih.AvailableQuantityAfter).IsRequired();
            entity.Property(ih => ih.ReferenceType).IsRequired().HasMaxLength(100);
            entity.Property(ih => ih.ReferenceId).IsRequired();
            entity.Property(ih => ih.PerformedBy).HasMaxLength(200);
            entity.Property(ih => ih.PerformedAt).IsRequired();
            entity.Property(ih => ih.Notes).HasMaxLength(1000);

            entity.HasOne(ih => ih.Warehouse)
                .WithMany()
                .HasForeignKey(ih => ih.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ih => ih.Unit)
                .WithMany()
                .HasForeignKey(ih => ih.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(ih => ih.WarehouseId);
            entity.HasIndex(ih => ih.UnitId);
            entity.HasIndex(ih => ih.ActionType);
            entity.HasIndex(ih => ih.PerformedAt);
            entity.HasIndex(ih => ih.ReferenceType);
            entity.HasIndex(ih => new { ih.ReferenceType, ih.ReferenceId });
        });

        // Configure Stocktake entity
        builder.Entity<Stocktake>(entity =>
        {
            entity.ToTable("Stocktakes");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.StocktakeNumber).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(s => s.ScopeType).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(s => s.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(s => s.CreatedByName).HasMaxLength(200);
            entity.Property(s => s.ApprovedByName).HasMaxLength(200);
            entity.Property(s => s.Notes).HasMaxLength(2000);

            entity.HasOne(s => s.Warehouse)
                .WithMany()
                .HasForeignKey(s => s.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.ScopeCategory)
                .WithMany()
                .HasForeignKey(s => s.ScopeCategoryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(s => !s.IsDeleted);

            entity.HasIndex(s => s.StocktakeNumber).IsUnique();
            entity.HasIndex(s => s.WarehouseId);
            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => s.Type);
            entity.HasIndex(s => s.CreatedAt);
        });

        // Configure StocktakeLine entity
        builder.Entity<StocktakeLine>(entity =>
        {
            entity.ToTable("StocktakeLines");
            entity.HasKey(sl => sl.Id);
            entity.Property(sl => sl.SystemQuantity).IsRequired();
            entity.Property(sl => sl.Difference).IsRequired();
            entity.Property(sl => sl.LineStatus).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(sl => sl.AdjustmentType).HasConversion<string>().HasMaxLength(30);
            entity.Property(sl => sl.GeneratedAdjustmentNumber).HasMaxLength(50);
            entity.Property(sl => sl.Notes).HasMaxLength(1000);

            entity.HasOne(sl => sl.Stocktake)
                .WithMany(s => s.Lines)
                .HasForeignKey(sl => sl.StocktakeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sl => sl.Unit)
                .WithMany()
                .HasForeignKey(sl => sl.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(sl => sl.StocktakeId);
            entity.HasIndex(sl => sl.UnitId);

            entity.HasQueryFilter(sl => !sl.IsDeleted);
        });

        // Configure StockTransfer entity
        builder.Entity<StockTransfer>(entity =>
        {
            entity.ToTable("StockTransfers");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TransferNumber).IsRequired().HasMaxLength(50);
            entity.Property(t => t.RequestedByName).HasMaxLength(200);
            entity.Property(t => t.Notes).HasMaxLength(2000);

            entity.HasOne(t => t.FromWarehouse)
                .WithMany()
                .HasForeignKey(t => t.FromWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ToWarehouse)
                .WithMany()
                .HasForeignKey(t => t.ToWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(t => !t.IsDeleted);

            entity.HasIndex(t => t.TransferNumber).IsUnique();
            entity.HasIndex(t => t.FromWarehouseId);
            entity.HasIndex(t => t.ToWarehouseId);
            entity.HasIndex(t => t.RequestedDate);
            entity.HasIndex(t => t.PurchaseRequestId);
        });

        // Configure Unit entity
        builder.Entity<Unit>(entity =>
        {
            entity.ToTable("SellingUnits");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Barcode).IsRequired().HasMaxLength(48);
            entity.Property(s => s.SellingPrice).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(s => s.SellingBarcode).IsRequired(false).HasMaxLength(48).HasDefaultValue(string.Empty);
            entity.Property(s => s.Cost).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(s => s.LowStockThreshold).IsRequired().HasDefaultValue(10);

            entity.HasIndex(s => s.Barcode).IsUnique();

            entity.HasOne(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.UnitOfMeasure)
                .WithMany()
                .HasForeignKey(s => s.UnitOfMeasureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(s => !s.IsDeleted);
        });

        // Configure UnitUnitType junction entity
        builder.Entity<UnitUnitType>(entity =>
        {
            entity.ToTable("UnitUnitTypes");
            entity.HasKey(uut => new { uut.UnitId, uut.UnitTypeId });

            entity.HasOne(uut => uut.Unit)
                .WithMany(u => u.UnitUnitTypes)
                .HasForeignKey(uut => uut.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(uut => uut.UnitType)
                .WithMany()
                .HasForeignKey(uut => uut.UnitTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure UnitSupplier junction entity
        builder.Entity<UnitSupplier>(entity =>
        {
            entity.ToTable("UnitSuppliers");
            entity.HasKey(usb => new { usb.UnitId, usb.SupplierId });

            entity.Property(usb => usb.Barcode).IsRequired().HasMaxLength(100);

            entity.HasOne(usb => usb.Unit)
                .WithMany(u => u.UnitSuppliers)
                .HasForeignKey(usb => usb.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(usb => usb.Supplier)
                .WithMany()
                .HasForeignKey(usb => usb.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure RefreshToken entity
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(rt => rt.Id);
            entity.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(128);
            entity.Property(rt => rt.ReplacedByTokenHash).HasMaxLength(128);
            entity.Property(rt => rt.CreatedByIp).HasMaxLength(64);
            entity.Property(rt => rt.RevokedByIp).HasMaxLength(64);
            entity.Property(rt => rt.ExpiresAt).IsRequired();
            entity.Property(rt => rt.UserId).IsRequired();

            entity.HasIndex(rt => rt.TokenHash).IsUnique();
            entity.HasIndex(rt => rt.UserId);
            entity.HasIndex(rt => rt.ExpiresAt);
        });

        // Configure StockTransferLine entity
        builder.Entity<StockTransferLine>(entity =>
        {
            entity.ToTable("StockTransferLines");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Quantity).IsRequired();
            entity.Property(l => l.Notes).HasMaxLength(1000);

            entity.HasOne(l => l.StockTransfer)
                .WithMany(t => t.Lines)
                .HasForeignKey(l => l.StockTransferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Unit)
                .WithMany()
                .HasForeignKey(l => l.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(l => l.StockTransferId);
            entity.HasIndex(l => l.UnitId);

            entity.HasQueryFilter(l => !l.IsDeleted);
        });

        // Configure PurchaseRequest entity
        builder.Entity<PurchaseRequest>(entity =>
        {
            entity.ToTable("PurchaseRequests");
            entity.HasKey(pr => pr.Id);
            entity.Property(pr => pr.RequestNumber).IsRequired().HasMaxLength(50);
            entity.Property(pr => pr.SupplySource).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(pr => pr.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(pr => pr.CreationMethod).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(pr => pr.ConvertedDocumentType).HasConversion<string>().HasMaxLength(30);
            entity.Property(pr => pr.RequestedByName).HasMaxLength(200);
            entity.Property(pr => pr.ApprovedByName).HasMaxLength(200);
            entity.Property(pr => pr.RejectedByName).HasMaxLength(200);
            entity.Property(pr => pr.RejectReason).HasMaxLength(2000);
            entity.Property(pr => pr.ConvertedDocumentReference).HasMaxLength(50);
            entity.Property(pr => pr.Notes).HasMaxLength(2000);

            entity.HasOne(pr => pr.RequestingWarehouse)
                .WithMany()
                .HasForeignKey(pr => pr.RequestingWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pr => pr.Supplier)
                .WithMany()
                .HasForeignKey(pr => pr.SupplierId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(pr => !pr.IsDeleted);

            entity.HasIndex(pr => pr.RequestNumber).IsUnique();
            entity.HasIndex(pr => pr.RequestingWarehouseId);
            entity.HasIndex(pr => pr.SupplierId);
            entity.HasIndex(pr => pr.Status);
            entity.HasIndex(pr => pr.SupplySource);
            entity.HasIndex(pr => pr.CreatedAt);
        });

        // Configure PurchaseRequestLine entity
        builder.Entity<PurchaseRequestLine>(entity =>
        {
            entity.ToTable("PurchaseRequestLines");
            entity.HasKey(prl => prl.Id);
            entity.Property(prl => prl.RequestedQuantity).IsRequired();
            entity.Property(prl => prl.Notes).HasMaxLength(1000);

            entity.HasOne(prl => prl.PurchaseRequest)
                .WithMany(pr => pr.Lines)
                .HasForeignKey(prl => prl.PurchaseRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(prl => prl.Unit)
                .WithMany()
                .HasForeignKey(prl => prl.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(prl => prl.PurchaseRequestId);
            entity.HasIndex(prl => prl.UnitId);

            entity.HasQueryFilter(prl => !prl.IsDeleted);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        string? currentUserName = null;
        
        if (_currentUserService != null)
        {
            try
            {
                var result = await _currentUserService.GetCurrentUserAsync();
                if (result.Item1 != Guid.Empty)
                {
                    currentUserName = result.Item2;
                }
            }
            catch
            {
                // If we can't get current user, continue without it
            }
        }

        UpdateAuditFields(currentUserName);

        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        string? currentUserName = null;
        
        if (_currentUserService != null)
        {
            try
            {
                var task = _currentUserService.GetCurrentUserAsync();
                task.Wait();
                var result = task.Result;
                if (result.Item1 != Guid.Empty)
                {
                    currentUserName = result.Item2;
                }
            }
            catch
            {
                // If we can't get current user, continue without it
            }
        }

        UpdateAuditFields(currentUserName);

        return base.SaveChanges();
    }

    private void UpdateAuditFields(string? currentUserName)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Only stamp CreatedAt when the caller hasn't supplied one.
                    // Offline-replayed records carry the actual client time and
                    // must not be overwritten with the sync moment.
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                    }
                    if (!string.IsNullOrEmpty(currentUserName))
                    {
                        entry.Entity.CreatedBy = currentUserName;
                    }

                    // No legacy closing-balance handling here; ensure DB is migrated to remove old column.
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(currentUserName))
                    {
                        entry.Entity.UpdatedBy = currentUserName;
                    }
                    break;

                case EntityState.Deleted:
                    if (entry.Entity is BaseAuditableEntity auditable)
                    {
                        entry.State = EntityState.Modified;
                        auditable.IsDeleted = true;
                        auditable.DeletedAt = DateTime.UtcNow;
                        if (!string.IsNullOrEmpty(currentUserName))
                        {
                            auditable.DeletedBy = currentUserName;
                        }
                    }
                    break;
            }
        }

        // Handle soft delete for Identity entities (ApplicationUser, ApplicationRole)
        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(currentUserName))
                {
                    entry.Entity.DeletedBy = currentUserName;
                }
            }
        }

        foreach (var entry in ChangeTracker.Entries<ApplicationRole>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(currentUserName))
                {
                    entry.Entity.DeletedBy = currentUserName;
                }
            }
        }
    }
}