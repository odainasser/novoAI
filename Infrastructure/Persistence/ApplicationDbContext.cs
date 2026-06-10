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
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // AI assistant (tool-calling) — turn log + governed plan library + no-answer queue
    public DbSet<AssistantInteraction> AssistantInteractions { get; set; }
    public DbSet<AssistantPlan> AssistantPlans { get; set; }
    public DbSet<AssistantNoAnswer> AssistantNoAnswers { get; set; }
    public DbSet<AssistantReportedAnswer> AssistantReportedAnswers { get; set; }

    // Apps integration module — registered client applications served by the assistant
    public DbSet<App> Apps { get; set; }

    // Atomic document-number counters
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

        // ── Apps integration module: registered client applications ──────
        builder.Entity<App>(entity =>
        {
            entity.ToTable("Apps");
            entity.Property(a => a.Code).IsRequired().HasMaxLength(50);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Description).HasMaxLength(500);
            entity.Property(a => a.BaseUrl).IsRequired().HasMaxLength(500);
            entity.Property(a => a.PersonaPrompt).HasMaxLength(500);
            entity.Property(a => a.Currency).IsRequired().HasMaxLength(10);
            entity.HasIndex(a => a.Code).IsUnique();
            entity.HasQueryFilter(a => !a.IsDeleted);
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
            entity.HasIndex(i => i.AppId);
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
            entity.HasIndex(p => new { p.AppId, p.MatchKey, p.Status });
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
            entity.HasIndex(c => new { c.AppId, c.ClusterKey }).IsUnique();
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
            entity.HasIndex(r => r.AppId);
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
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                    }
                    if (!string.IsNullOrEmpty(currentUserName))
                    {
                        entry.Entity.CreatedBy = currentUserName;
                    }
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
