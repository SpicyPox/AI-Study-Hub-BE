using System.Security.Claims;
using AIStudyHub.Api.DTOs.Auth;
using AIStudyHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    // Bước 1: gửi mã OTP về email. Chưa tạo tài khoản.
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        await authService.RegisterSendOtpAsync(req);
        return Ok(new { message = "Mã xác minh đã được gửi đến email của bạn." });
    }

    // Bước 2: xác minh OTP -> tạo tài khoản và trả token (đăng nhập luôn).
    [HttpPost("register/verify")]
    [EnableRateLimiting("auth")]
    public async Task<AuthResponse> RegisterVerify(RegisterVerifyRequest req) =>
        await authService.RegisterVerifyAsync(req);

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<AuthResponse> Login(LoginRequest req) =>
        await authService.LoginAsync(req);

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req)
    {
        var response = await authService.RefreshAsync(req.RefreshToken);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await authService.LogoutAsync(UserId());
        return Ok();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<UserDto> GetMe() => await authService.GetMeAsync(UserId());

    [Authorize]
    [HttpGet("me/storage")]
    public async Task<StorageDto> GetMyStorage() => await authService.GetStorageAsync(UserId());

    [Authorize]
    [HttpPatch("me")]
    public async Task<UserDto> UpdateMe(UpdateMeRequest req) =>
        await authService.UpdateMeAsync(UserId(), req);

    [HttpPost("google")]
    [EnableRateLimiting("auth")]
    public async Task<GoogleAuthResponse> LoginWithGoogle(GoogleLoginRequest req) =>
        await authService.LoginWithGoogleAsync(req);

    [HttpPost("google/verify")]
    [EnableRateLimiting("auth")]
    public async Task<AuthResponse> GoogleVerify(GoogleVerifyRequest req) =>
        await authService.GoogleVerifyAsync(req);

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        await authService.ForgotPasswordAsync(req);
        return Ok(new { message = "Nếu email tồn tại trong hệ thống, mã xác thực đã được gửi." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        await authService.ResetPasswordAsync(req);
        return Ok(new { message = "Mật khẩu đã được thay đổi thành công." });
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
