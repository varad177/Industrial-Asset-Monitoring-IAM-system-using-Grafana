using IAM.Application.Common.Exceptions;
using IAM.Application.DTOs;
using IAM.Application.Interfaces;
using IAM.Domain.Entities;
using IAM.Domain.Interfaces;

namespace IAM.Infrastructure.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtService _jwt;

    public AuthService(IUnitOfWork uow, IPasswordHasher hasher, IJwtService jwt)
    {
        _uow = uow;
        _hasher = hasher;
        _jwt = jwt;
    }

    // ── Register ─────────────────────────────────────────────
    public async Task<AuthResponseDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Validate
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            errors["username"] = ["Username must be at least 3 characters."];

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            errors["email"] = ["A valid email is required."];

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            errors["password"] = ["Password must be at least 8 characters."];

        if (request.Password != request.ConfirmPassword)
            errors["confirmPassword"] = ["Passwords do not match."];

        if (errors.Count > 0)
            throw new ValidationException(errors);

        // Uniqueness checks
        if (await _uow.Users.ExistsByEmailAsync(request.Email.ToLower(), ct))
            throw new ConflictException("An account with this email already exists.");

        if (await _uow.Users.ExistsByUsernameAsync(request.Username.ToLower(), ct))
            throw new ConflictException("This username is already taken.");

        // Create user
        var hash = _hasher.Hash(request.Password);
        var user = User.Create(request.Username, request.Email, hash);

        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshTokenValue = _jwt.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(user.Id, refreshTokenValue);
        user.AddRefreshToken(refreshToken);

        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return new AuthResponseDto(
            AccessToken: accessToken,
            RefreshToken: refreshTokenValue,
            AccessTokenExpiry: _jwt.GetAccessTokenExpiry(),
            User: new UserDto(user.Id, user.Username, user.Email, user.Role)
        );
    }

    // ── Login ────────────────────────────────────────────────
    public async Task<AuthResponseDto> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var identifier = request.EmailOrUsername.Trim().ToLower();

        // Find user by email or username
        User? user = identifier.Contains('@')
            ? await _uow.Users.GetByEmailAsync(identifier, ct)
            : await _uow.Users.GetByUsernameAsync(identifier, ct);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials.");

        if (!user.IsActive)
            throw new UnauthorizedException("Your account has been deactivated.");

        // Issue tokens
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshTokenValue = _jwt.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(user.Id, refreshTokenValue);
        user.AddRefreshToken(refreshToken);
        await _uow.AddRefreshTokenAsync(refreshToken, ct);

        await _uow.SaveChangesAsync(ct);

        return new AuthResponseDto(
            AccessToken: accessToken,
            RefreshToken: refreshTokenValue,
            AccessTokenExpiry: _jwt.GetAccessTokenExpiry(),
            User: new UserDto(user.Id, user.Username, user.Email, user.Role)
        );
    }

    // ── Refresh Token ────────────────────────────────────────
    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var existingToken = await _uow.Users.GetRefreshTokenAsync(request.Token, ct);

        if (existingToken is null || !existingToken.IsActive)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var user = await _uow.Users.GetByIdAsync(existingToken.UserId, ct)
            ?? throw new UnauthorizedException("User not found.");

        // Revoke old token and issue new tokens
        var newRefreshValue = _jwt.GenerateRefreshToken();
        var newRefreshToken = RefreshToken.Create(user.Id, newRefreshValue);
        existingToken.Revoke("Rotated");
        user.AddRefreshToken(newRefreshToken);
        await _uow.AddRefreshTokenAsync(newRefreshToken, ct);

        var accessToken = _jwt.GenerateAccessToken(user);
        await _uow.SaveChangesAsync(ct);

        return new AuthResponseDto(
            AccessToken: accessToken,
            RefreshToken: newRefreshValue,
            AccessTokenExpiry: _jwt.GetAccessTokenExpiry(),
            User: new UserDto(user.Id, user.Username, user.Email, user.Role)
        );
    }
}
