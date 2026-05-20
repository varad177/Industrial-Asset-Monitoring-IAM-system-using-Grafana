namespace IAM.Application.DTOs;

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string ConfirmPassword
);

public record LoginRequest(
    string EmailOrUsername,
    string Password
);

public record RefreshTokenRequest(string Token);
