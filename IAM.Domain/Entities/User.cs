using IAM.Domain.Common;

namespace IAM.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = "Viewer";
    public bool IsActive { get; private set; } = true;
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    // EF Core constructor
    private User() { }

    public static User Create(string username, string email, string passwordHash, string role = "Viewer")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Username = username.Trim().ToLower(),
            Email = email.Trim().ToLower(),
            PasswordHash = passwordHash,
            Role = role
        };
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        SetUpdatedAt();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdatedAt();
    }

    public void AddRefreshToken(RefreshToken token)
    {
        // Revoke old tokens
        // foreach (var oldToken in RefreshTokens.Where(t => t.IsActive))
        //     oldToken.Revoke();

        RefreshTokens.Add(token);
    }
}