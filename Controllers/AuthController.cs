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
        await authService.RegisterVerifyAsync(req, UserAgent(), IpAddress());

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<LoginResponse> Login(LoginRequest req) =>
        await authService.LoginAsync(req, UserAgent(), IpAddress());

    // Bước 2 khi tài khoản có bật 2FA: gửi kèm twoFactorToken nhận được từ /login + mã TOTP.
    [HttpPost("2fa/login-verify")]
    [EnableRateLimiting("auth")]
    public async Task<LoginResponse> TwoFactorLoginVerify(TwoFactorVerifyRequest req) =>
        await authService.TwoFactorLoginVerifyAsync(req, UserAgent(), IpAddress());

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
        await authService.LogoutAsync(UserId(), SessionId());
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
        await authService.LoginWithGoogleAsync(req, UserAgent(), IpAddress());

    [HttpPost("google/verify")]
    [EnableRateLimiting("auth")]
    public async Task<AuthResponse> GoogleVerify(GoogleVerifyRequest req) =>
        await authService.GoogleVerifyAsync(req, UserAgent(), IpAddress());

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

    // ─── Xác thực 2 bước (TOTP) ─────────────────────────────────────────────────
    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<TwoFactorSetupResponse> TwoFactorSetup() =>
        await authService.TwoFactorSetupAsync(UserId());

    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<IActionResult> TwoFactorEnable(TwoFactorEnableRequest req)
    {
        await authService.TwoFactorEnableAsync(UserId(), req);
        return Ok(new { message = "Đã bật xác thực 2 bước." });
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> TwoFactorDisable(TwoFactorDisableRequest req)
    {
        await authService.TwoFactorDisableAsync(UserId(), req);
        return Ok(new { message = "Đã tắt xác thực 2 bước." });
    }

    // ─── Quản lý phiên đăng nhập trên nhiều thiết bị ────────────────────────────
    [Authorize]
    [HttpGet("sessions")]
    public async Task<List<SessionDto>> GetSessions() =>
        await authService.GetSessionsAsync(UserId(), SessionId());

    [Authorize]
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id)
    {
        await authService.RevokeSessionAsync(UserId(), id);
        return Ok();
    }

    [Authorize]
    [HttpPost("sessions/revoke-others")]
    public async Task<IActionResult> RevokeOtherSessions()
    {
        await authService.RevokeOtherSessionsAsync(UserId(), SessionId());
        return Ok();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Guid? SessionId() =>
        Guid.TryParse(User.FindFirstValue("sid"), out var sid) ? sid : null;

    private string? UserAgent() => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    private string? IpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
