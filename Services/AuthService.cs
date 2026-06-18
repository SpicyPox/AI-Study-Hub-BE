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

public class AuthService(AppDbContext db, IConfiguration config, EmailService emailService)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        var lowerEmail = req.Email.ToLower();
        if (await db.Users.AnyAsync(u => u.Email == lowerEmail))
            throw new InvalidOperationException("Email đã được sử dụng.");

        if (await db.Users.AnyAsync(u => u.Username == req.Name))
            throw new InvalidOperationException("Tên người dùng đã được sử dụng.");

        var user = new User
        {
            Username = req.Name,
            Email = lowerEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLower())
            ?? throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        return await BuildAuthResponseAsync(user);
    }

    public async Task<string> RefreshAsync(string refreshToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.RefreshTokenExpiry > DateTime.UtcNow)
            ?? throw new UnauthorizedAccessException("Refresh token không hợp lệ hoặc đã hết hạn.");

        return GenerateAccessToken(user);
    }

    public async Task LogoutAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            await db.SaveChangesAsync();
        }
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

        if (req.Name is not null && req.Name != user.Username)
        {
            if (await db.Users.AnyAsync(u => u.Username == req.Name))
                throw new InvalidOperationException("Tên người dùng đã được sử dụng.");
            user.Username = req.Name;
        }

        if (req.Email is not null)
        {
            var lowerEmail = req.Email.ToLower();
            if (lowerEmail != user.Email)
            {
                if (await db.Users.AnyAsync(u => u.Email == lowerEmail))
                    throw new InvalidOperationException("Email đã được sử dụng.");
                user.Email = lowerEmail;
            }
        }

        if (req.NewPassword is not null)
        {
            if (string.IsNullOrEmpty(req.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
                throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        }

        await db.SaveChangesAsync();
        return ToDto(user);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await db.SaveChangesAsync();

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
            new Claim(ClaimTypes.Role, user.Role.ToString()),
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

    public async Task ForgotPasswordAsync(ForgotPasswordRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLower())
            ?? throw new KeyNotFoundException("Email không tồn tại trong hệ thống.");

        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = code,
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow
        };

        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var subject = "[AI Study Hub] Mã xác thực đặt lại mật khẩu";
        var body = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 5px; max-width: 600px;'>
                <h2 style='color: #4A90E2;'>Đặt lại mật khẩu</h2>
                <p>Xin chào <b>{user.Username}</b>,</p>
                <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản tại AI Study Hub.</p>
                <p>Dưới đây là mã xác thực của bạn (có hiệu lực trong 15 phút):</p>
                <div style='background: #f4f4f4; padding: 15px; font-size: 24px; font-weight: bold; letter-spacing: 5px; text-align: center; border-radius: 4px; color: #333;'>
                    {code}
                </div>
                <p>Nếu bạn không gửi yêu cầu này, vui lòng bỏ qua email này.</p>
                <br>
                <p>Trân trọng,<br>Đội ngũ AI Study Hub</p>
            </div>";

        await emailService.SendEmailAsync(user.Email, subject, body);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest req)
    {
        var resetToken = await db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == req.Token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
            ?? throw new InvalidOperationException("Mã xác thực không hợp lệ, đã được sử dụng hoặc đã hết hạn.");

        resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        resetToken.IsUsed = true;

        await db.SaveChangesAsync();
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Username, u.Email, u.Role.ToString());
}
