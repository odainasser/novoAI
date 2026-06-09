using Application.Common.Models;
using Application.Features.Shifts;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class ShiftService : IShiftService
{
    private readonly ApplicationDbContext _context;

    public ShiftService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasActiveShiftAsync(Guid cashierId)
    {
        return await _context.Shifts.AnyAsync(s => s.CashierId == cashierId && s.Status == ShiftStatus.Active);
    }

    public async Task<ShiftDto> StartShiftAsync(Guid cashierId, string? cashierName, StartShiftRequest request)
    {
        // Ensure cashier has no active shift
        var active = await _context.Shifts.FirstOrDefaultAsync(s => s.CashierId == cashierId && s.Status == ShiftStatus.Active);
        if (active != null)
        {
            throw new InvalidOperationException("An active shift already exists for this cashier");
        }

        // Get the cashier's current active warehouse
        Guid? warehouseId = null;
        string? warehouseNameEn = null;
        string? warehouseNameAr = null;

        var cashierUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == cashierId);
        if (cashierUser?.WarehouseId.HasValue == true)
        {
            warehouseId = cashierUser.WarehouseId;
            var warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId);
            if (warehouse != null)
            {
                warehouseNameEn = warehouse.NameEn;
                warehouseNameAr = warehouse.NameAr;
            }
        }

        // Offline-replayed starts carry the actual open time in ClientStartedAt.
        var startTime = request.ClientStartedAt ?? DateTime.UtcNow;

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            CashierId = cashierId,
            CashierName = cashierName,
            StartTime = startTime,
            CreatedAt = startTime,
            CashIn = request.CashIn,
            Comments = request.Comments,
            Status = ShiftStatus.Active,
            WarehouseId = warehouseId,
            WarehouseNameEn = warehouseNameEn,
            WarehouseNameAr = warehouseNameAr
        };

        _context.Shifts.Add(shift);
        await _context.SaveChangesAsync();

        return MapToDto(shift);
    }

    public async Task<ShiftDto> EndShiftAsync(Guid shiftId, EndShiftRequest request)
    {
        var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift == null) throw new InvalidOperationException("Shift not found");
        if (shift.Status == ShiftStatus.Completed) throw new InvalidOperationException("Shift already completed");

        // Offline-replayed ends carry the actual close time in ClientEndedAt.
        var endTime = request.ClientEndedAt ?? DateTime.UtcNow;

        // Calculate totals from orders during shift
        var orders = await _context.Orders
            .Where(o => o.CashierId == shift.CashierId && o.CreatedAt >= shift.StartTime && o.CreatedAt <= endTime)
            .ToListAsync();

        shift.TotalSales = orders.Where(o => o.Status == Domain.Enums.OrderStatus.Completed || o.Status == Domain.Enums.OrderStatus.PartialRefunded).Sum(o => o.Total);
        // Returns only considered for refunded/returned orders
        shift.TotalReturns = orders.Where(o => o.Status == Domain.Enums.OrderStatus.Refunded).Sum(o => o.Total);

        shift.CashOut = request.CashOut;
        // ClosingBalance is no longer stored; compute expected closing here and compare with provided inputs if desired.
        // We no longer persist ClosingBalance to DB.
        shift.EndTime = endTime;
        shift.Comments = string.IsNullOrWhiteSpace(shift.Comments) ? request.Comments : $"{shift.Comments}\n{request.Comments}";
        shift.Status = ShiftStatus.Completed;

        await _context.SaveChangesAsync();

        return MapToDto(shift);
    }

    public async Task<PaginatedList<ShiftDto>> GetShiftsByCashierAsync(Guid cashierId, int pageNumber = 1, int pageSize = 10, Guid? warehouseId = null)
    {
        var query = _context.Shifts.Where(s => s.CashierId == cashierId).AsQueryable();
        if (warehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == warehouseId.Value);
        query = query.OrderByDescending(s => s.StartTime);
        var count = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedList<ShiftDto>(items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<PaginatedList<ShiftDto>> GetAllShiftsAsync(int pageNumber = 1, int pageSize = 20, string? status = null, string? search = null, Guid? cashierId = null, Guid? warehouseId = null, IEnumerable<Guid>? warehouseIds = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Shifts.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            // support both name and numeric values, ignore case for names
            if (Enum.TryParse<ShiftStatus>(status, true, out var st))
            {
                query = query.Where(s => s.Status == st);
            }
            else if (int.TryParse(status, out var si))
            {
                if (Enum.IsDefined(typeof(ShiftStatus), si))
                {
                    var st2 = (ShiftStatus)si;
                    query = query.Where(s => s.Status == st2);
                }
            }
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(s => (s.CashierName ?? string.Empty).Contains(search));
        }

        if (cashierId.HasValue)
        {
            query = query.Where(s => s.CashierId == cashierId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(s => s.WarehouseId == warehouseId.Value);
        }

        // Multi-warehouse filter (used by Branch Panel — see IOrderService for context).
        if (warehouseIds is not null)
        {
            var ids = warehouseIds.Select(g => (Guid?)g).ToList();
            if (ids.Count == 0)
            {
                return new PaginatedList<ShiftDto>(new List<ShiftDto>(), 0, pageNumber, pageSize);
            }
            query = query.Where(s => ids.Contains(s.WarehouseId));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(s => s.StartTime >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            var inclusiveTo = toDate.Value.Date == toDate.Value ? toDate.Value.AddDays(1) : toDate.Value;
            query = query.Where(s => s.StartTime < inclusiveTo);
        }

        query = query.OrderByDescending(s => s.StartTime);
        var count = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedList<ShiftDto>(items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<ShiftDto?> GetShiftByIdAsync(Guid id)
    {
        var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == id);
        return shift == null ? null : MapToDto(shift);
    }

    public async Task<byte[]> ExportShiftsToExcelAsync(
        string? status = null,
        string? search = null,
        Guid? cashierId = null,
        Guid? warehouseId = null,
        bool isArabic = false,
        IEnumerable<Guid>? warehouseIds = null)
    {
        var query = _context.Shifts.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<ShiftStatus>(status, true, out var st))
            {
                query = query.Where(s => s.Status == st);
            }
            else if (int.TryParse(status, out var si) && Enum.IsDefined(typeof(ShiftStatus), si))
            {
                var st2 = (ShiftStatus)si;
                query = query.Where(s => s.Status == st2);
            }
        }

        if (!string.IsNullOrEmpty(search))
            query = query.Where(s => (s.CashierName ?? string.Empty).Contains(search));

        if (cashierId.HasValue)
            query = query.Where(s => s.CashierId == cashierId.Value);

        if (warehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == warehouseId.Value);

        // Multi-warehouse filter (mirrors GetAllShiftsAsync) — used by the Branch Panel
        // to scope export to all warehouses owned by a branch.
        if (warehouseIds is not null)
        {
            var ids = warehouseIds.Select(g => (Guid?)g).ToList();
            if (ids.Count == 0)
            {
                using var emptyBook = new ClosedXML.Excel.XLWorkbook();
                emptyBook.Worksheets.Add("Shifts");
                using var emptyStream = new MemoryStream();
                emptyBook.SaveAs(emptyStream);
                return emptyStream.ToArray();
            }
            query = query.Where(s => ids.Contains(s.WarehouseId));
        }

        var rows = await query
            .OrderByDescending(s => s.StartTime)
            .Select(s => new
            {
                s.CashierName,
                s.WarehouseNameEn,
                s.WarehouseNameAr,
                s.StartTime,
                s.EndTime,
                s.TotalSales,
                s.TotalReturns,
                s.CashIn,
                s.CashOut,
                s.Status
            })
            .ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Shifts");

        var headers = new[]
        {
            isArabic ? "أمين الصندوق" : "Cashier",
            isArabic ? "المتجر" : "Store",
            isArabic ? "وقت البدء" : "Start Time",
            isArabic ? "وقت الانتهاء" : "End Time",
            isArabic ? "إجمالي المبيعات" : "Total Sales",
            isArabic ? "إجمالي المرتجعات" : "Total Returns",
            isArabic ? "النقد الوارد" : "Cash In",
            isArabic ? "النقد الصادر" : "Cash Out",
            isArabic ? "الحالة" : "Status"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(0xF3, 0xF4, 0xF6);
        headerRow.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Left;

        var rowIndex = 2;
        foreach (var r in rows)
        {
            sheet.Cell(rowIndex, 1).Value = r.CashierName ?? string.Empty;
            sheet.Cell(rowIndex, 2).Value = isArabic ? (r.WarehouseNameAr ?? string.Empty) : (r.WarehouseNameEn ?? string.Empty);
            sheet.Cell(rowIndex, 3).Value = r.StartTime;
            sheet.Cell(rowIndex, 3).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            if (r.EndTime.HasValue)
            {
                sheet.Cell(rowIndex, 4).Value = r.EndTime.Value;
                sheet.Cell(rowIndex, 4).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            }
            sheet.Cell(rowIndex, 5).Value = r.TotalSales;
            sheet.Cell(rowIndex, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(rowIndex, 6).Value = r.TotalReturns;
            sheet.Cell(rowIndex, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(rowIndex, 7).Value = r.CashIn;
            sheet.Cell(rowIndex, 7).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(rowIndex, 8).Value = r.CashOut;
            sheet.Cell(rowIndex, 8).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(rowIndex, 9).Value = r.Status.ToString();
            rowIndex++;
        }

        sheet.RangeUsed()?.SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        foreach (var col in sheet.ColumnsUsed())
        {
            if (col.Width > 50) col.Width = 50;
            if (col.Width < 12) col.Width = 12;
        }

        if (isArabic)
        {
            sheet.RightToLeft = true;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private ShiftDto MapToDto(Shift s)
    {
        return new ShiftDto
        {
            Id = s.Id,
            CashierId = s.CashierId,
            CashierName = s.CashierName,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            TotalSales = s.TotalSales,
            TotalReturns = s.TotalReturns,
            CashIn = s.CashIn,
            CashOut = s.CashOut,
            Status = s.Status,
            Comments = s.Comments,
            CreatedAt = s.CreatedAt,
            WarehouseId = s.WarehouseId,
            WarehouseNameEn = s.WarehouseNameEn,
            WarehouseNameAr = s.WarehouseNameAr
        };
    }
}
