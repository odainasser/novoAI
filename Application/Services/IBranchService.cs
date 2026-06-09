using Application.Common.Models;
using Application.Features.Branches;

namespace Application.Services;

public interface IBranchService
{
    Task<PaginatedList<BranchDto>> GetAllBranchesAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null);
    Task<List<BranchDto>> GetActiveBranchesAsync();
    Task<List<BranchDto>> GetBranchesAssignedToUserAsync(Guid userId);
    Task<BranchDto?> GetBranchByIdAsync(Guid id);
    Task<BranchDto> CreateBranchAsync(CreateBranchRequest request);
    Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchRequest request);
    Task DeleteBranchAsync(Guid id);
    Task<bool> CheckBranchExistsAsync(string nameEn, string nameAr, Guid? excludeBranchId = null);

    // ===== Branch-scoping helpers =====
    //
    // Used by the Branch Panel endpoints (and any other surface that needs to
    // gate data on a user's branch assignment). These do not duplicate other
    // service logic — they only resolve the relationships in UserBranches and
    // Warehouses that other domain services can then filter by.

    /// <summary>True if the user is a member of the branch via UserBranches.</summary>
    Task<bool> IsUserAssignedToBranchAsync(Guid userId, Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>The branch's primary back-office warehouse (Lookup code MW), or null if none.</summary>
    Task<BranchWarehouseInfo?> GetBranchWarehouseAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>Every warehouse Id owned by the branch (MS store + MW back-office).</summary>
    Task<List<Guid>> GetWarehouseIdsForBranchAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>Every user Id assigned to the branch via UserBranches.</summary>
    Task<List<Guid>> GetUserIdsForBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
}

public class BranchWarehouseInfo
{
    public Guid Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
}
