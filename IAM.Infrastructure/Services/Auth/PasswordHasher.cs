using IAM.Application.Interfaces;
using BCryptNet = BCrypt.Net.BCrypt;

namespace IAM.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
        => BCryptNet.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash)
        => BCryptNet.Verify(password, hash);
}