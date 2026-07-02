using System.ComponentModel.DataAnnotations;

namespace AIStudyHub.Api.DTOs.Auth;

public record RegisterRequest(
    [Required] string Name,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);

// Bước 2 của đăng ký: xác minh mã OTP 6 số đã gửi về email.
public record RegisterVerifyRequest(
    [Required, EmailAddress] string Email,
    [Required] string Otp
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record RefreshRequest(
    [Required] string RefreshToken
);

public record RefreshResponse(string AccessToken, string RefreshToken);

public record UpdateMeRequest(
    string? Name, 
    [EmailAddress] string? Email, 
    string? CurrentPassword, 
    [MinLength(8)] string? NewPassword
);

public record UserDto(Guid Id, string Name, string Email, string Role);
public record AuthResponse(UserDto User, string AccessToken, string RefreshToken);

// GET /auth/me/storage → dung lượng đã dùng / tổng dung lượng (bytes)
public record StorageDto(long UsedBytes, long TotalCapacityBytes);

public record GoogleLoginRequest(
    [Required] string Credential  // Google authorization code từ frontend (flow: auth-code)
);

// Verify OTP để hoàn tất đăng ký tài khoản Google mới
public record GoogleVerifyRequest(
    [Required, EmailAddress] string Email,
    [Required] string Otp
);

// Trả về khi Google email chưa có tài khoản → cần OTP
public record GoogleOtpRequiredResponse(string Status, string Email);

// Trả về khi đăng nhập thành công (bao gồm cả Google)
public record GoogleAuthResponse(string Status, UserDto? User, string? AccessToken, string? RefreshToken);

public record ForgotPasswordRequest(
    [Required, EmailAddress] string Email
);

public record ResetPasswordRequest(
    [Required] string Token, 
    [Required, MinLength(8)] string NewPassword
);
