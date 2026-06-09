using Application.Common.Models;
using Application.Features.Shifts;

namespace Application.Services;

public interface IShiftService
{
    Task<ShiftDto> StartShiftAsync(Guid cashierId, string? cashierName, StartShiftRequest request);
    Task<ShiftDto> EndShiftAsync(Guid shiftId, EndShiftRequest request);
    Task<PaginatedList<ShiftDto>> GetShiftsByCashierAsync(Guid cashierId, int pageNumber = 1, int pageSize = 10, Guid? warehouseId = null);
    Task<PaginatedList<ShiftDto>> GetAllShiftsAsync(int pageNumber = 1, int pageSize = 20, string? status = null, string? search = null, Guid? cashierId = null, Guid? warehouseId = null, IEnumerable<Guid>? warehouseIds = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<ShiftDto?> GetShiftByIdAsync(Guid id);
    Task<bool> HasActiveShiftAsync(Guid cashierId);

    Task<byte[]> ExportShiftsToExcelAsync(
        string? status = null,
        string? search = null,
        Guid? cashierId = null,
        Guid? warehouseId = null,
        bool isArabic = false,
        IEnumerable<Guid>? warehouseIds = null);
}
