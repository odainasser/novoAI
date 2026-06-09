namespace Application.Common.Interfaces;

public interface ICurrentUserService
{
    Task<(Guid UserId, string UserName)> GetCurrentUserAsync();
}
