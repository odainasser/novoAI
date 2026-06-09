using Web.Models.Common;
using Web.Models.Shifts;

namespace Web.Services;

public interface IShiftService
{
    Task<ShiftDto> StartShiftAsync(StartShiftRequest request);
    Task<ShiftDto> EndShiftAsync(Guid id, EndShiftRequest request);
    Task<PaginatedList<ShiftDto>> GetMyShiftsAsync(int pageNumber = 1, int pageSize = 10, Guid? warehouseId = null);
    Task<PaginatedList<ShiftDto>> GetAllShiftsAsync(int pageNumber = 1, int pageSize = 20, string? status = null, string? search = null, Guid? cashierId = null, Guid? warehouseId = null, Guid? branchId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<bool> HasActiveShiftAsync();

    Task<byte[]> ExportShiftsToExcelAsync(
        string? status = null,
        string? search = null,
        Guid? cashierId = null,
        Guid? warehouseId = null,
        bool isArabic = false,
        Guid? branchId = null);
}
