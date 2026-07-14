using System.Security.Cryptography;
using System.Text;

namespace AIStudyHub.Api.Services;

/// <summary>
/// Xác thực 2 bước kiểu TOTP (RFC 6238) tương thích Google/Microsoft Authenticator.
/// Không dùng thư viện ngoài — chỉ HMAC-SHA1 + Base32, đủ cho chuẩn TOTP mặc định (30s, 6 số).
/// </summary>
public class TotpService(IConfiguration config)
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    // Cho phép lệch ±1 bước (±30s) để bù trừ đồng hồ thiết bị không khớp tuyệt đối.
    private const int WindowSteps = 1;

    /// <summary>Sinh secret ngẫu nhiên 20 byte (160 bit), mã hoá Base32 để hiển thị/quét QR.</summary>
    public string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public string BuildOtpAuthUri(string secret, string email)
    {
        var issuer = Uri.EscapeDataString("AI Study Hub");
        var label = Uri.EscapeDataString($"AI Study Hub:{email}");
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&digits={Digits}&period={StepSeconds}";
    }

    public bool VerifyCode(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits) return false;

        var key = Base32Decode(base32Secret);
        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;

        for (var offset = -WindowSteps; offset <= WindowSteps; offset++)
        {
            if (ComputeCode(key, currentStep + offset) == code) return true;
        }
        return false;
    }

    private static string ComputeCode(byte[] key, long step)
    {
        var stepBytes = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(stepBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(stepBytes);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | ((hash[offset + 1] & 0xFF) << 16)
                     | ((hash[offset + 2] & 0xFF) << 8)
                     | (hash[offset + 3] & 0xFF);

        var code = binary % (int)Math.Pow(10, Digits);
        return code.ToString().PadLeft(Digits, '0');
    }

    // ─── Base32 (RFC 4648, không đệm '=') ───────────────────────────────────────
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        int bits = 0, value = 0;
        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Base32Alphabet[(value >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(Base32Alphabet[(value << (5 - bits)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        base32 = base32.Trim().TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int bits = 0, value = 0;
        foreach (var c in base32)
        {
            var idx = Base32Alphabet.IndexOf(c);
            if (idx < 0) continue;
            value = (value << 5) | idx;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.ToArray();
    }

    // ─── Mã hoá secret khi lưu DB (AES-256-GCM, key rút ra từ Jwt:Key hiện có) ──
    public string Encrypt(string plaintext)
    {
        var key = DeriveKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, Encoding.UTF8.GetBytes(plaintext), cipher, tag);

        return Convert.ToBase64String(nonce) + ":" + Convert.ToBase64String(cipher) + ":" + Convert.ToBase64String(tag);
    }

    public string Decrypt(string payload)
    {
        var parts = payload.Split(':');
        var nonce = Convert.FromBase64String(parts[0]);
        var cipher = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(DeriveKey(), tag.Length);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }

    private byte[] DeriveKey() => SHA256.HashData(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
}
