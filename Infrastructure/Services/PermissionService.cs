using Application.Features.Roles;
using Application.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await _context.Permissions
            .OrderBy(p => p.Module)
            .ThenBy(p => p.NameEn) // Changed from Name
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.NameEn, // Map to NameEn
                NameEn = p.NameEn,
                NameAr = p.NameAr,
                Description = p.DescriptionEn, // Map to DescriptionEn
                DescriptionEn = p.DescriptionEn,
                DescriptionAr = p.DescriptionAr,
                Module = p.Module
            })
            .ToListAsync(cancellationToken);

        return permissions;
    }

    public async Task<IEnumerable<PermissionDto>> GetPermissionsByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var permissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => new PermissionDto
            {
                Id = rp.Permission.Id,
                Code = rp.Permission.Code,
                Name = rp.Permission.NameEn, // Map to NameEn
                NameEn = rp.Permission.NameEn,
                NameAr = rp.Permission.NameAr,
                Description = rp.Permission.DescriptionEn, // Map to DescriptionEn
                DescriptionEn = rp.Permission.DescriptionEn,
                DescriptionAr = rp.Permission.DescriptionAr,
                Module = rp.Permission.Module
            })
            .ToListAsync(cancellationToken);

        return permissions;
    }
}
