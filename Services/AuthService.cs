using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Auth;
using AIStudyHub.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AIStudyHub.Api.Services;

public class AuthService(AppDbContext db, IConfiguration config)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            throw new InvalidOperationException("Email đã được sử dụng.");

        var user = new User
        {
            Name = req.Name,
            Email = req.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLower())
            ?? throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Tài khoản đã bị khoá.");

        return BuildAuthResponse(user);
    }

    public async Task<string> RefreshAsync(string refreshToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == refreshToken && u.RefreshTokenExpiry > DateTime.UtcNow)
            ?? throw new UnauthorizedAccessException("Refresh token không hợp lệ hoặc đã hết hạn.");

        return GenerateAccessToken(user);
    }

    public async Task LogoutAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await db.SaveChangesAsync();
    }

    public async Task<UserDto> GetMeAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Người dùng không tồn tại.");
        return ToDto(user);
    }

    public async Task<UserDto> UpdateMeAsync(Guid userId, UpdateMeRequest req)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Người dùng không tồn tại.");

        if (req.Name is not null) user.Name = req.Name;
        if (req.Email is not null) user.Email = req.Email.ToLower();

        if (req.NewPassword is not null)
        {
            if (string.IsNullOrEmpty(req.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
                throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        }

        await db.SaveChangesAsync();
        return ToDto(user);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        db.SaveChanges();

        return new AuthResponse(ToDto(user), accessToken, refreshToken);
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static UserDto ToDto(User u) => new(u.Id, u.Name, u.Email, u.Role);
}
