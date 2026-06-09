using Application.Services;
using Domain.Constants;
using System.Security.Claims;

namespace Api.Authorization;

// Shared helper used by every endpoint that accepts an optional `branchId`
// query parameter. Provides one funnel for the membership check so the rule
// is applied identically everywhere.
//
// Admins (anyone holding the `branches.read` permission) bypass the check —
// they can filter by any branch. Non-admins must be members of the branch
// via UserBranches.
public static class BranchScoping
{
    public sealed class Result
    {
        public IResult? FailureResult { get; init; }
        public List<Guid> WarehouseIds { get; init; } = new();
        public Guid? PrimaryWarehouseId { get; init; }
    }

    /// <summary>
    /// Resolves the warehouseIds for the given branch and validates the
    /// caller's membership. Returns Result.FailureResult when the caller is
    /// not allowed (Unauthorized or Forbid) so the endpoint can short-circuit.
    /// On success FailureResult is null and the caller can use WarehouseIds.
    /// </summary>
    public static async Task<Result> ScopeAsync(
        HttpContext httpContext,
        IBranchService branchService,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var userIdString = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return new Result { FailureResult = Results.Unauthorized() };
        }

        // Admins (or anyone with branches.read) can filter by any branch without
        // being a member — Admin Reports etc. legitimately need cross-branch access.
        var hasGlobalAccess = httpContext.User.HasClaim("permission", Permissions.BranchesRead);

        if (!hasGlobalAccess)
        {
            var isMember = await branchService.IsUserAssignedToBranchAsync(userId, branchId, cancellationToken);
            if (!isMember)
            {
                return new Result { FailureResult = Results.Forbid() };
            }
        }

        var warehouseIds = await branchService.GetWarehouseIdsForBranchAsync(branchId, cancellationToken);
        var primary = await branchService.GetBranchWarehouseAsync(branchId, cancellationToken);

        return new Result
        {
            WarehouseIds = warehouseIds,
            PrimaryWarehouseId = primary?.Id
        };
    }
}
