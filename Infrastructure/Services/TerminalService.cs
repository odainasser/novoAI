using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.UserLogs;
using Application.Features.Terminals;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class TerminalService : ITerminalService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;

    public TerminalService(
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<TerminalDto>> GetAllTerminalsAsync(
        int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? branchId = null)
    {
        var query = _context.Terminals
            .Include(d => d.Branch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(d =>
                d.NameEn.ToLower().Contains(s) ||
                d.NameAr.ToLower().Contains(s) ||
                (d.ComputerIp != null && d.ComputerIp.ToLower().Contains(s)) ||
                (d.PrinterIp != null && d.PrinterIp.ToLower().Contains(s)) ||
                (d.PaymentMachineIp != null && d.PaymentMachineIp.ToLower().Contains(s)));
        }

        if (isActive.HasValue)
            query = query.Where(d => d.IsActive == isActive.Value);

        if (branchId.HasValue)
            query = query.Where(d => d.BranchId == branchId.Value);

        query = query.OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<TerminalDto>(items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<List<TerminalDto>> GetActiveTerminalsAsync()
    {
        var terminals = await _context.Terminals
            .Include(d => d.Branch)
            .Where(d => d.IsActive)
            .OrderBy(d => d.NameEn)
            .ToListAsync();

        return terminals.Select(MapToDto).ToList();
    }

    public async Task<TerminalDto?> GetTerminalByIdAsync(Guid id)
    {
        var terminal = await _context.Terminals
            .Include(d => d.Branch)
            .FirstOrDefaultAsync(d => d.Id == id);

        return terminal == null ? null : MapToDto(terminal);
    }

    public async Task<TerminalDto> CreateTerminalAsync(CreateTerminalRequest request)
    {
        var terminal = new Terminal
        {
            Id = Guid.NewGuid(),
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            ComputerIp = request.ComputerIp,
            PrinterIp = request.PrinterIp,
            PaymentMachineIp = request.PaymentMachineIp,
            BranchId = request.BranchId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Terminals.Add(terminal);
        await _context.SaveChangesAsync();

        // Reload navigation properties
        await _context.Entry(terminal).Reference(d => d.Branch).LoadAsync();

        var (uid, uname) = await _currentUserService.GetCurrentUserAsync();
        if (uid != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = uid,
                UserName = uname,
                Action = AuditAction.Created,
                EntityName = "Terminal",
                EntityId = terminal.Id.ToString(),
                Details = null
            });
        }

        return MapToDto(terminal);
    }

    public async Task<TerminalDto> UpdateTerminalAsync(Guid id, UpdateTerminalRequest request)
    {
        var terminal = await _context.Terminals
            .Include(d => d.Branch)
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException($"Terminal with ID {id} not found.");

        terminal.NameEn = request.NameEn;
        terminal.NameAr = request.NameAr;
        terminal.ComputerIp = request.ComputerIp;
        terminal.PrinterIp = request.PrinterIp;
        terminal.PaymentMachineIp = request.PaymentMachineIp;
        terminal.BranchId = request.BranchId;
        terminal.IsActive = request.IsActive;
        terminal.UpdatedAt = DateTime.UtcNow;

        _context.Terminals.Update(terminal);
        await _context.SaveChangesAsync();

        await _context.Entry(terminal).Reference(d => d.Branch).LoadAsync();

        var (uid, uname) = await _currentUserService.GetCurrentUserAsync();
        if (uid != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = uid,
                UserName = uname,
                Action = AuditAction.Updated,
                EntityName = "Terminal",
                EntityId = terminal.Id.ToString(),
                Details = null
            });
        }

        return MapToDto(terminal);
    }

    public async Task DeleteTerminalAsync(Guid id)
    {
        var terminal = await _context.Terminals.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException($"Terminal with ID {id} not found.");

        _context.Terminals.Remove(terminal);
        await _context.SaveChangesAsync();

        var (uid, uname) = await _currentUserService.GetCurrentUserAsync();
        if (uid != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = uid,
                UserName = uname,
                Action = AuditAction.Deleted,
                EntityName = "Terminal",
                EntityId = terminal.Id.ToString(),
                Details = null
            });
        }
    }

    public async Task<bool> CheckTerminalExistsAsync(string nameEn, string nameAr, Guid? excludeTerminalId = null)
    {
        var query = _context.Terminals.Where(d =>
            d.NameEn.ToLower() == nameEn.ToLower() ||
            d.NameAr.ToLower() == nameAr.ToLower());

        if (excludeTerminalId.HasValue)
            query = query.Where(d => d.Id != excludeTerminalId.Value);

        return await query.AnyAsync();
    }

    private static TerminalDto MapToDto(Terminal d) => new()
    {
        Id = d.Id,
        NameEn = d.NameEn,
        NameAr = d.NameAr,
        ComputerIp = d.ComputerIp,
        PrinterIp = d.PrinterIp,
        PaymentMachineIp = d.PaymentMachineIp,
        BranchId = d.BranchId,
        BranchNameEn = d.Branch?.NameEn,
        BranchNameAr = d.Branch?.NameAr,
        IsActive = d.IsActive,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };
}
