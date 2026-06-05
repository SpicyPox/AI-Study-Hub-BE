namespace AIStudyHub.Api.DTOs.Auth;

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record UpdateMeRequest(string? Name, string? Email, string? CurrentPassword, string? NewPassword);

public record UserDto(Guid Id, string Name, string Email, string Role);
public record AuthResponse(UserDto User, string AccessToken, string RefreshToken);
