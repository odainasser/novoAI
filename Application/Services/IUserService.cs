using Application.Common.Models;
using Application.Features.Users;

namespace Application.Services;

public interface IUserService
{
    Task<PaginatedList<UserResponse>> GetAllUsersAsync(int pageNumber, int pageSize, string? role = null, string? search = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<UserResponse?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserResponse> AssignRolesToUserAsync(Guid userId, AssignRolesRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserResponse>> GetActiveUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
}
