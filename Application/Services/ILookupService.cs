using Application.Common.Models;
using Application.Features.Lookups;

namespace Application.Services;

public interface ILookupService
{
    Task<PaginatedList<LookupDto>> GetAllLookupsAsync(int pageNumber, int pageSize, string? parentCode = null, string? search = null, bool? isActive = null);
    Task<List<LookupDto>> GetLookupsByParentAsync(string parentCode);
    Task<List<LookupDto>> GetRootLookupsAsync();
    Task<LookupDto?> GetLookupByIdAsync(Guid id);
    Task<LookupDto> CreateLookupAsync(CreateLookupRequest request);
    Task<LookupDto> UpdateLookupAsync(Guid id, UpdateLookupRequest request);
    Task DeleteLookupAsync(Guid id);
    Task<(bool CodeExists, bool NameEnExists, bool NameArExists)> CheckLookupExistsAsync(string code, string nameEn, string nameAr, Guid? excludeLookupId = null);
}
