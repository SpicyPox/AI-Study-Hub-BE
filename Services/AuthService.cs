using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Auth;
using AIStudyHub.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace AIStudyHub.Api.Services;

public class AuthService(AppDbContext db, IConfiguration config, EmailService emailService, IMemoryCache cache)
{
    // Thông tin đăng ký tạm đang chờ xác minh OTP. Lưu trong IMemoryCache (hết hạn 15 phút),
    // CHƯA ghi vào DB cho đến khi người dùng nhập đúng mã -> không tạo tài khoản "rác" chưa xác minh.
    private sealed class PendingRegistration
    {
        public required string Name { get; init; }
        public required string Email { get; init; }
        public required string PasswordHash { get; init; }
        public required string Otp { get; init; }
        public int Attempts { get; set; }
    }

    private const int MaxOtpAttempts = 5;
    private static string RegCacheKey(string lowerEmail) => $"reg-otp:{lowerEmail}";

    // Bước 1: kiểm tra email/tên chưa dùng, sinh OTP 6 số, gửi email, lưu pending vào cache.
    // KHÔNG tạo user ở bước này.
    public async Task RegisterSendOtpAsync(RegisterRequest req)
    {
        var lowerEmail = req.Email.ToLower();
        if (await db.Users.AnyAsync(u => u.Email == lowerEmail))
            throw new InvalidOperationException("Email đã được sử dụng.");

        if (await db.Users.AnyAsync(u => u.Username == req.Name))
            throw new InvalidOperationException("Tên người dùng đã được sử dụng.");

        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        // Hash mật khẩu ngay để không lưu plaintext trong cache.
        cache.Set(RegCacheKey(lowerEmail), new PendingRegistration
        {
            Name = req.Name,
            Email = lowerEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Otp = code,
            Attempts = 0
        }, TimeSpan.FromMinutes(15));

        var subject = "[AI Study Hub] Mã xác minh đăng ký tài khoản";
        var body = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 5px; max-width: 600px;'>
                <h2 style='color: #4A90E2;'>Xác minh đăng ký</h2>
                <p>Xin chào <b>{req.Name}</b>,</p>
                <p>Cảm ơn bạn đã đăng ký tài khoản tại AI Study Hub.</p>
                <p>Dưới đây là mã xác minh của bạn (có hiệu lực trong 15 phút):</p>
                <div style='background: #f4f4f4; padding: 15px; font-size: 24px; font-weight: bold; letter-spacing: 5px; text-align: center; border-radius: 4px; color: #333;'>
                    {code}
                </div>
                <p>Nhập mã này vào trang đăng ký để hoàn tất việc tạo tài khoản.</p>
                <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.</p>
                <br>
                <p>Trân trọng,<br>Đội ngũ AI Study Hub</p>
            </div>";

        await emailService.SendEmailAsync(lowerEmail, subject, body);
    }

    // Bước 2: xác minh OTP. Đúng -> tạo user thật + trả token (đăng nhập luôn).
    public async Task<AuthResponse> RegisterVerifyAsync(RegisterVerifyRequest req)
    {
        var lowerEmail = req.Email.ToLower();
        var key = RegCacheKey(lowerEmail);

        if (!cache.TryGetValue(key, out PendingRegistration? pending) || pending is null)
            throw new InvalidOperationException("Mã xác minh không hợp lệ hoặc đã hết hạn. Vui lòng đăng ký lại.");

        if (pending.Otp != req.Otp.Trim())
        {
            pending.Attempts++;
            if (pending.Attempts >= MaxOtpAttempts)
            {
                cache.Remove(key);
                throw new InvalidOperationException("Bạn đã nhập sai mã quá nhiều lần. Vui lòng đăng ký lại.");
            }
            throw new InvalidOperationException("Mã xác minh không đúng.");
        }

        // Kiểm tra lại tính duy nhất phòng trường hợp có người chiếm email/tên trong lúc chờ.
        if (await db.Users.AnyAsync(u => u.Email == lowerEmail))
        {
            cache.Remove(key);
            throw new InvalidOperationException("Email đã được sử dụng.");
        }
        if (await db.Users.AnyAsync(u => u.Username == pending.Name))
        {
            cache.Remove(key);
            throw new InvalidOperationException("Tên người dùng đã được sử dụng.");
        }

        var defaultRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "user");
        var user = new User
        {
            Username = pending.Name,
            Email = pending.Email,
            PasswordHash = pending.PasswordHash, // đã hash sẵn ở bước 1
            RoleId = defaultRole?.Id
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        cache.Remove(key);

        if (user.Role == null && user.RoleId.HasValue)
        {
            user.Role = defaultRole;
        }

        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());

        bool isValid = false;
        if (user != null)
        {
            isValid = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
        }
        else
        {
            // Dummy verify to prevent timing attack / user enumeration
            BCrypt.Net.BCrypt.Verify(req.Password, "$2a$12$L7m12W1X2Y3Z4A5B6C7D8E9F0G1H2I3J4K5L6M7N8O9P0Q1R2S3Tu");
        }

        if (user == null || !isValid)
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        var response = await BuildAuthResponseAsync(user);

        // Gửi email thông báo đăng nhập (không block response)
        _ = SendLoginNotificationAsync(user.Email, user.Username);

        return response;
    }

    private async Task SendLoginNotificationAsync(string email, string username)
    {
        try
        {
            var time = DateTime.Now.ToString("HH:mm dd/MM/yyyy");
            var subject = "[AI Study Hub] Thông báo đăng nhập tài khoản";
            var body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 5px; max-width: 600px;'>
                    <h2 style='color: #4A90E2;'>Thông báo đăng nhập</h2>
                    <p>Xin chào <b>{username}</b>,</p>
                    <p>Tài khoản của bạn vừa được đăng nhập vào lúc <b>{time}</b>.</p>
                    <p>Nếu đây không phải bạn, hãy <b>đổi mật khẩu ngay</b> để bảo vệ tài khoản.</p>
                    <br>
                    <p>Trân trọng,<br>Đội ngũ AI Study Hub</p>
                </div>";
            await emailService.SendEmailAsync(email, subject, body);
        }
        catch { /* không block login nếu gửi mail lỗi */ }
    }

    public async Task<RefreshResponse> RefreshAsync(string refreshToken)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.RefreshTokenExpiry > DateTime.UtcNow)
            ?? throw new UnauthorizedAccessException("Refresh token không hợp lệ hoặc đã hết hạn.");

        var newAccessToken = GenerateAccessToken(user);
        var newRefreshToken = GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await db.SaveChangesAsync();

        return new RefreshResponse(newAccessToken, newRefreshToken);
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
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("Người dùng không tồn tại.");
        return ToDto(user);
    }

    public async Task<UserDto> UpdateMeAsync(Guid userId, UpdateMeRequest req)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId)
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
        // Ensure Role is loaded for generating access token
        if (user.Role == null && user.RoleId.HasValue)
        {
            await db.Entry(user).Reference(u => u.Role).LoadAsync();
        }

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
            new Claim(ClaimTypes.Role, user.Role?.Name ?? "user"),
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
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());
        if (user == null)
        {
            // Return early to prevent user enumeration
            return;
        }

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

    // Cache key cho Google pending signup
    private static string GooglePendingKey(string lowerEmail) => $"google-pending:{lowerEmail}";

    private sealed class PendingGoogleSignup
    {
        public required string Name  { get; init; }
        public required string Email { get; init; }
        public required string Otp   { get; init; }
        public int Attempts { get; set; }
    }

    // Trả về (lowerEmail, name) từ Google authorization code
    private async Task<(string Email, string Name)> ExchangeGoogleCodeAsync(string code)
    {
        var clientId     = config["GoogleAuth:ClientId"]!;
        var clientSecret = config["GoogleAuth:ClientSecret"]!;

        using var http = new HttpClient();
        var tokenRes = await http.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"]          = code,
                ["client_id"]     = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"]  = "postmessage",
                ["grant_type"]    = "authorization_code",
            }));

        if (!tokenRes.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Không thể xác thực với Google.");

        var tokenData = await tokenRes.Content.ReadFromJsonAsync<GoogleTokenResponse>()
            ?? throw new UnauthorizedAccessException("Không đọc được token Google.");

        var infoRes = await http.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={tokenData.IdToken}");
        if (!infoRes.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Google ID token không hợp lệ.");

        var payload = await infoRes.Content.ReadFromJsonAsync<GoogleTokenPayload>()
            ?? throw new UnauthorizedAccessException("Không đọc được thông tin Google.");

        if (payload.Aud != clientId)
            throw new UnauthorizedAccessException("Google token không hợp lệ.");

        return (payload.Email.ToLower(), payload.Name);
    }

    /// <summary>
    /// Trả về GoogleAuthResponse:
    ///   - status "otp_required" nếu email chưa có tài khoản → gửi OTP
    ///   - status "ok" nếu tài khoản đã tồn tại → đăng nhập + gửi mail thông báo
    /// </summary>
    public async Task<GoogleAuthResponse> LoginWithGoogleAsync(GoogleLoginRequest req)
    {
        var (lowerEmail, name) = await ExchangeGoogleCodeAsync(req.Credential);

        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == lowerEmail);

        if (user == null)
        {
            // Tài khoản mới → gửi OTP xác minh trước khi tạo
            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var displayName = string.IsNullOrWhiteSpace(name) ? lowerEmail.Split('@')[0] : name;

            cache.Set(GooglePendingKey(lowerEmail), new PendingGoogleSignup
            {
                Name     = displayName,
                Email    = lowerEmail,
                Otp      = otp,
                Attempts = 0
            }, TimeSpan.FromMinutes(15));

            var subject = "[AI Study Hub] Xác minh đăng ký bằng Google";
            var body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 5px; max-width: 600px;'>
                    <h2 style='color: #4A90E2;'>Xác minh tài khoản Google</h2>
                    <p>Xin chào <b>{displayName}</b>,</p>
                    <p>Bạn đang đăng ký tài khoản AI Study Hub bằng Google (<b>{lowerEmail}</b>).</p>
                    <p>Mã xác minh của bạn (hiệu lực 15 phút):</p>
                    <div style='background: #f4f4f4; padding: 15px; font-size: 24px; font-weight: bold; letter-spacing: 5px; text-align: center; border-radius: 4px; color: #333;'>
                        {otp}
                    </div>
                    <p>Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email này.</p>
                    <br><p>Trân trọng,<br>Đội ngũ AI Study Hub</p>
                </div>";

            await emailService.SendEmailAsync(lowerEmail, subject, body);

            return new GoogleAuthResponse("otp_required", null, null, null);
        }

        // Tài khoản đã có → đăng nhập + gửi thông báo
        var authResp = await BuildAuthResponseAsync(user);
        _ = SendLoginNotificationAsync(user.Email, user.Username);

        return new GoogleAuthResponse("ok", authResp.User, authResp.AccessToken, authResp.RefreshToken);
    }

    /// <summary>Xác minh OTP Google signup → tạo tài khoản + đăng nhập</summary>
    public async Task<AuthResponse> GoogleVerifyAsync(GoogleVerifyRequest req)
    {
        var lowerEmail = req.Email.ToLower();
        var key = GooglePendingKey(lowerEmail);

        if (!cache.TryGetValue(key, out PendingGoogleSignup? pending) || pending is null)
            throw new InvalidOperationException("Mã xác minh không hợp lệ hoặc đã hết hạn. Vui lòng thử lại.");

        if (pending.Otp != req.Otp.Trim())
        {
            pending.Attempts++;
            if (pending.Attempts >= MaxOtpAttempts)
            {
                cache.Remove(key);
                throw new InvalidOperationException("Nhập sai quá nhiều lần. Vui lòng thử lại.");
            }
            throw new InvalidOperationException("Mã xác minh không đúng.");
        }

        cache.Remove(key);

        if (await db.Users.AnyAsync(u => u.Email == lowerEmail))
            throw new InvalidOperationException("Email đã được sử dụng.");

        var displayName = pending.Name;
        if (await db.Users.AnyAsync(u => u.Username == displayName))
            displayName = $"{displayName}_{Guid.NewGuid().ToString()[..4]}";

        var defaultRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "user");
        var user = new User
        {
            Username     = displayName,
            Email        = lowerEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
            RoleId       = defaultRole?.Id,
            Role         = defaultRole
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return await BuildAuthResponseAsync(user);
    }

    private sealed class GoogleTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id_token")]
        public string IdToken { get; set; } = "";
    }

    private sealed class GoogleTokenPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("aud")]
        public string Aud { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string Email { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    public async Task<StorageDto> GetStorageAsync(Guid userId)
    {
        var storage = await db.UserStorages.FirstOrDefaultAsync(s => s.UserId == userId);
        long used  = storage?.UsedBytes ?? 0;
<<<<<<< HEAD
        long total = storage?.TotalCapacityBytes ?? 10485760L; // 10 MB mặc định (demo)
=======
        long total = storage?.TotalCapacityBytes ?? 10485760L; // 10 MB mặc định
>>>>>>> 835bc4dc5e5d5a689bf8a69025ce29027161dff7
        return new StorageDto(used, total);
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Username, u.Email, u.Role?.Name ?? "user");
}
