using System.ComponentModel.DataAnnotations;

namespace AIStudyHub.Api.DTOs.Auth;

public record RegisterRequest(
    [Required] string Name, 
    [Required, EmailAddress] string Email, 
    [Required, MinLength(6)] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email, 
    [Required] string Password
);

public record RefreshRequest(
    [Required] string RefreshToken
);

public record UpdateMeRequest(
    string? Name, 
    [EmailAddress] string? Email, 
    string? CurrentPassword, 
    [MinLength(6)] string? NewPassword
);

public record UserDto(Guid Id, string Name, string Email, string Role);
public record AuthResponse(UserDto User, string AccessToken, string RefreshToken);

public record ForgotPasswordRequest(
    [Required, EmailAddress] string Email
);

public record ResetPasswordRequest(
    [Required] string Token, 
    [Required, MinLength(6)] string NewPassword
);
