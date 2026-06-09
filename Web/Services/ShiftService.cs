using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Shifts;
using Web.Models.Enums;

namespace Web.Services;

public class ShiftService : IShiftService
{
    private readonly HttpClient _http;

    public ShiftService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ShiftDto> StartShiftAsync(StartShiftRequest request)
    {
        var res = await _http.PostAsJsonAsync("/api/shifts/start", request);
        await res.HandleErrorAsync();
        return (await res.Content.ReadFromJsonAsync<ShiftDto>())!;
    }

    public async Task<ShiftDto> EndShiftAsync(Guid id, EndShiftRequest request)
    {
        var res = await _http.PostAsJsonAsync($"/api/shifts/{id}/end", request);
        await res.HandleErrorAsync();
        return (await res.Content.ReadFromJsonAsync<ShiftDto>())!;
    }

    public async Task<PaginatedList<ShiftDto>> GetMyShiftsAsync(int pageNumber = 1, int pageSize = 10, Guid? warehouseId = null)
    {
        var url = $"/api/shifts/my?pageNumber={pageNumber}&pageSize={pageSize}";
        if (warehouseId.HasValue) url += $"&warehouseId={warehouseId.Value}";
        var res = await _http.GetFromJsonAsync<PaginatedList<ShiftDto>>(url);
        return res!;
    }

    public async Task<PaginatedList<ShiftDto>> GetAllShiftsAsync(int pageNumber = 1, int pageSize = 20, string? status = null, string? search = null, Guid? cashierId = null, Guid? warehouseId = null, Guid? branchId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = $"/api/shifts?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(status)) query += $"&status={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrEmpty(search)) query += $"&search={Uri.EscapeDataString(search)}";
        if (cashierId.HasValue) query += $"&cashierId={cashierId.Value}";
        if (warehouseId.HasValue) query += $"&warehouseId={warehouseId.Value}";
        if (branchId.HasValue) query += $"&branchId={branchId.Value}";
        if (fromDate.HasValue) query += $"&fromDate={Uri.EscapeDataString(fromDate.Value.ToString("o"))}";
        if (toDate.HasValue) query += $"&toDate={Uri.EscapeDataString(toDate.Value.ToString("o"))}";
        var res = await _http.GetFromJsonAsync<PaginatedList<ShiftDto>>(query);
        return res!;
    }

    // Backwards-compatible overload for callers that expect the old signature
    public Task<PaginatedList<ShiftDto>> GetAllShiftsAsync(int pageNumber, int pageSize)
    {
        return GetAllShiftsAsync(pageNumber, pageSize, null, null);
    }

    public async Task<bool> HasActiveShiftAsync()
    {
        // Get recent shifts for current user and check for an active one
        var res = await _http.GetFromJsonAsync<PaginatedList<ShiftDto>>($"/api/shifts/my?pageNumber=1&pageSize=10");
        if (res == null || res.Items == null || !res.Items.Any()) return false;

        // Prefer explicit enum status, but fall back to EndTime == null as indicator of active shift
        if (res.Items.Any(s => s.Status == ShiftStatus.Active)) return true;
        if (res.Items.Any(s => s.EndTime == null)) return true;
        return false;
    }

    public async Task<byte[]> ExportShiftsToExcelAsync(
        string? status = null,
        string? search = null,
        Guid? cashierId = null,
        Guid? warehouseId = null,
        bool isArabic = false,
        Guid? branchId = null)
    {
        var query = "/api/shifts/export";
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(status)) parts.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrEmpty(search)) parts.Add($"search={Uri.EscapeDataString(search)}");
        if (cashierId.HasValue) parts.Add($"cashierId={cashierId.Value}");
        if (warehouseId.HasValue) parts.Add($"warehouseId={warehouseId.Value}");
        if (branchId.HasValue) parts.Add($"branchId={branchId.Value}");
        if (isArabic) parts.Add("ar=true");
        if (parts.Count > 0) query += "?" + string.Join("&", parts);

        var response = await _http.GetAsync(query);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }
}
