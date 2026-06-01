using IAM.Domain.Entities;

namespace IAM.Application.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateClientCredentialsToken(string clientId);
    string GenerateRefreshToken();
    DateTime GetAccessTokenExpiry();
}