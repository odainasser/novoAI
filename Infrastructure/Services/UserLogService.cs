using Application.Common.Models;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class UserLogService : IUserLogService
{
    private readonly IUserLogRepository _repository;
    private readonly ApplicationDbContext _context;

    public UserLogService(IUserLogRepository repository, ApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task LogAsync(CreateUserLogRequest request)
    {
        var log = new UserLog
        {
            UserId = request.UserId,
            UserName = request.UserName,
            Action = request.Action,
            EntityName = request.EntityName,
            EntityId = request.EntityId,
            Details = request.Details,
            Timestamp = DateTime.UtcNow
        };

        await _repository.AddAsync(log);
        await _repository.SaveChangesAsync();
    }

    public async Task<PaginatedList<UserLogDto>> GetLogsAsync(int pageNumber, int pageSize, Guid? userId = null, string? entityName = null, string? entityId = null, string? culture = null, string? search = null, string? matchActions = null, string? matchEntities = null)
    {
        var (items, totalCount) = await _repository.GetPagedLogsAsync(pageNumber, pageSize, userId, entityName, entityId, search, matchActions, matchEntities);

        var dtos = items.Select(l => new UserLogDto
        {
            Id = l.Id,
            UserId = l.UserId,
            UserName = l.UserName,
            Action = l.Action.ToString(),
            EntityName = l.EntityName,
            EntityId = l.EntityId,
            Details = l.Details,
            Timestamp = l.Timestamp
        }).ToList();

        await ResolveEntityDisplayNamesAsync(dtos, culture);

        return new PaginatedList<UserLogDto>(dtos, totalCount, pageNumber, pageSize);
    }

    private async Task ResolveEntityDisplayNamesAsync(List<UserLogDto> dtos, string? culture)
    {
        var isArabic = culture?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) == true;

        var entities = dtos
            .Where(d => !string.IsNullOrEmpty(d.EntityName) && !string.IsNullOrEmpty(d.EntityId))
            .Select(d => (d.EntityName!, d.EntityId!))
            .Distinct()
            .ToList();

        if (!entities.Any()) return;

        var nameMap = new Dictionary<(string, string), string>();

        var grouped = entities.GroupBy(e => e.Item1, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var ids = group
                .Select(g => Guid.TryParse(g.Item2, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (!ids.Any()) continue;

            switch (group.Key.ToLowerInvariant())
            {
                case "product":
                    var products = await _context.Products
                        .Where(p => ids.Contains(p.Id))
                        .Select(p => new { p.Id, p.NameAr, p.NameEn })
                        .ToListAsync();
                    foreach (var p in products)
                        nameMap[(group.Key, p.Id.ToString())] = isArabic ? p.NameAr : p.NameEn;
                    break;

                case "unit":
                    var units = await _context.Units
                        .Include(u => u.Product)
                        .Include(u => u.UnitOfMeasure)
                        .Where(u => ids.Contains(u.Id))
                        .Select(u => new { u.Id, u.Barcode, u.Quantity, ProductNameAr = u.Product != null ? u.Product.NameAr : string.Empty, ProductNameEn = u.Product != null ? u.Product.NameEn : string.Empty, UomNameAr = u.UnitOfMeasure != null ? u.UnitOfMeasure.NameAr : string.Empty, UomNameEn = u.UnitOfMeasure != null ? u.UnitOfMeasure.NameEn : string.Empty })
                        .ToListAsync();
                    foreach (var u in units)
                    {
                        var pName = isArabic ? u.ProductNameAr : u.ProductNameEn;
                        var uomName = isArabic ? u.UomNameAr : u.UomNameEn;
                        nameMap[(group.Key, u.Id.ToString())] = $"{pName} ({uomName}, ×{u.Quantity})";
                    }
                    break;

                case "category":
                    var categories = await _context.Categories
                        .Where(c => ids.Contains(c.Id))
                        .Select(c => new { c.Id, c.NameAr, c.NameEn })
                        .ToListAsync();
                    foreach (var c in categories)
                        nameMap[(group.Key, c.Id.ToString())] = isArabic ? c.NameAr : c.NameEn;
                    break;

                case "user":
                    var users = await _context.Users
                        .Where(u => ids.Contains(u.Id))
                        .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                        .ToListAsync();
                    foreach (var u in users)
                    {
                        var fullName = $"{u.FirstName} {u.LastName}".Trim();
                        nameMap[(group.Key, u.Id.ToString())] = !string.IsNullOrEmpty(fullName) ? fullName : u.Email ?? "";
                    }
                    break;

                case "role":
                    var roles = await _context.DomainRoles
                        .Where(r => ids.Contains(r.Id))
                        .Select(r => new { r.Id, r.Name })
                        .ToListAsync();
                    foreach (var r in roles)
                        nameMap[(group.Key, r.Id.ToString())] = r.Name;
                    break;

                case "lookup":
                    var lookups = await _context.Lookups
                        .Where(l => ids.Contains(l.Id))
                        .Select(l => new { l.Id, l.NameAr, l.NameEn })
                        .ToListAsync();
                    foreach (var l in lookups)
                        nameMap[(group.Key, l.Id.ToString())] = isArabic ? l.NameAr : l.NameEn;
                    break;

                case "supplier":
                    var suppliers = await _context.Suppliers
                        .Where(s => ids.Contains(s.Id))
                        .Select(s => new { s.Id, s.NameAr, s.NameEn })
                        .ToListAsync();
                    foreach (var s in suppliers)
                        nameMap[(group.Key, s.Id.ToString())] = isArabic ? s.NameAr : s.NameEn;
                    break;

                case "branch":
                    var branches = await _context.Branches
                        .Where(m => ids.Contains(m.Id))
                        .Select(m => new { m.Id, m.NameAr, m.NameEn })
                        .ToListAsync();
                    foreach (var m in branches)
                        nameMap[(group.Key, m.Id.ToString())] = isArabic ? m.NameAr : m.NameEn;
                    break;

                case "warehouse":
                    var warehouses = await _context.Warehouses
                        .Where(w => ids.Contains(w.Id))
                        .Select(w => new { w.Id, w.NameAr, w.NameEn })
                        .ToListAsync();
                    foreach (var w in warehouses)
                        nameMap[(group.Key, w.Id.ToString())] = isArabic ? w.NameAr : w.NameEn;
                    break;

                case "terminal":
                    var terminals = await _context.Terminals
                        .Where(t => ids.Contains(t.Id))
                        .Select(t => new { t.Id, t.NameAr, t.NameEn })
                        .ToListAsync();
                    foreach (var t in terminals)
                        nameMap[(group.Key, t.Id.ToString())] = isArabic ? t.NameAr : t.NameEn;
                    break;

                case "cashier":
                    var cashiers = await _context.Users
                        .Where(u => ids.Contains(u.Id))
                        .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                        .ToListAsync();
                    foreach (var c in cashiers)
                    {
                        var name = $"{c.FirstName} {c.LastName}".Trim();
                        nameMap[(group.Key, c.Id.ToString())] = !string.IsNullOrEmpty(name) ? name : c.Email ?? "";
                    }
                    break;

                case "goodsreceivingnote":
                    var grns = await _context.GoodsReceivingNotes
                        .Where(g => ids.Contains(g.Id))
                        .Select(g => new { g.Id, g.GRNNumber })
                        .ToListAsync();
                    foreach (var g in grns)
                        nameMap[(group.Key, g.Id.ToString())] = g.GRNNumber;
                    break;

                case "stockadjustment":
                    var adjustments = await _context.StockAdjustments
                        .Where(a => ids.Contains(a.Id))
                        .Select(a => new { a.Id, a.AdjustmentNumber })
                        .ToListAsync();
                    foreach (var a in adjustments)
                        nameMap[(group.Key, a.Id.ToString())] = a.AdjustmentNumber;
                    break;

                case "stocktransfer":
                    var transfers = await _context.StockTransfers
                        .Where(t => ids.Contains(t.Id))
                        .Select(t => new { t.Id, t.TransferNumber })
                        .ToListAsync();
                    foreach (var t in transfers)
                        nameMap[(group.Key, t.Id.ToString())] = t.TransferNumber;
                    break;

                case "request":
                    var requests = await _context.Requests
                        .Where(r => ids.Contains(r.Id))
                        .Select(r => new { r.Id, r.Type, r.Status, r.ProductName })
                        .ToListAsync();
                    foreach (var r in requests)
                    {
                        nameMap[(group.Key, r.Id.ToString())] = string.IsNullOrWhiteSpace(r.ProductName)
                            ? $"{r.Type}"
                            : $"{r.Type} - {r.ProductName}";
                    }
                    break;
            }
        }

        foreach (var dto in dtos)
        {
            if (!string.IsNullOrEmpty(dto.EntityName) && !string.IsNullOrEmpty(dto.EntityId)
                && nameMap.TryGetValue((dto.EntityName, dto.EntityId), out var displayName))
            {
                dto.EntityDisplayName = displayName;
            }
        }
    }
}
