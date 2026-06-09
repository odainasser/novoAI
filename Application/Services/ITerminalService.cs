using Application.Common.Models;
using Application.Features.Terminals;

namespace Application.Services;

public interface ITerminalService
{
    Task<PaginatedList<TerminalDto>> GetAllTerminalsAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? branchId = null);
    Task<List<TerminalDto>> GetActiveTerminalsAsync();
    Task<TerminalDto?> GetTerminalByIdAsync(Guid id);
    Task<TerminalDto> CreateTerminalAsync(CreateTerminalRequest request);
    Task<TerminalDto> UpdateTerminalAsync(Guid id, UpdateTerminalRequest request);
    Task DeleteTerminalAsync(Guid id);
    Task<bool> CheckTerminalExistsAsync(string nameEn, string nameAr, Guid? excludeTerminalId = null);
}
