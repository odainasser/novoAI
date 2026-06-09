using Web.Models.Common;
using Web.Models.Branches;

namespace Web.Services;

public interface IBranchService
{
    Task<PaginatedList<BranchDto>> GetAllBranchesAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null);
    Task<List<BranchDto>> GetActiveBranchesAsync();
    Task<List<BranchDto>> GetMyBranchesAsync();
    Task<BranchWarehouseDto?> GetBranchWarehouseAsync(Guid branchId);
    Task<BranchDto?> GetBranchByIdAsync(Guid id);
    Task<BranchDto> CreateBranchAsync(CreateBranchRequest request);
    Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchRequest request);
    Task DeleteBranchAsync(Guid id);
    Task<bool> CheckBranchExistsAsync(string nameEn, string nameAr, Guid? excludeBranchId = null);
}
