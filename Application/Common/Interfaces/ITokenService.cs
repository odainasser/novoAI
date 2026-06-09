namespace Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateJwtToken(Guid userId, string email, string fullName, IList<string> roles, IList<string> permissions);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
    int AccessTokenLifetimeSeconds { get; }
    int RefreshTokenLifetimeDays { get; }
}
