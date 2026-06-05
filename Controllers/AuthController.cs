using System.Security.Claims;
using AIStudyHub.Api.DTOs.Auth;
using AIStudyHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<AuthResponse> Register(RegisterRequest req) =>
        await authService.RegisterAsync(req);

    [HttpPost("login")]
    public async Task<AuthResponse> Login(LoginRequest req) =>
        await authService.LoginAsync(req);

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req)
    {
        var token = await authService.RefreshAsync(req.RefreshToken);
        return Ok(new { accessToken = token });
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

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
