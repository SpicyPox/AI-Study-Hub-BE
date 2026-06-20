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
    [HttpPost("register")]
    public async Task<AuthResponse> Register(RegisterRequest req) =>
        await authService.RegisterAsync(req);

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
    [HttpPatch("me")]
    public async Task<UserDto> UpdateMe(UpdateMeRequest req) =>
        await authService.UpdateMeAsync(UserId(), req);

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
