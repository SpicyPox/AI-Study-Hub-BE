using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

/// <summary>
/// Lưu thông tin cốt lõi. Dùng UUID thay vì ID (1,2,3) để bảo mật, chống đối thủ đoán số lượng user và dễ scale server.
/// </summary>
public partial class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    /// <summary>
    /// Dùng VARCHAR(255) kết hợp UNIQUE INDEX LOWER() để ép hệ thống hiểu &quot;Email@gmail&quot; và &quot;email@gmail&quot; là một, chống tạo 2 tài khoản trùng lặp. Đã bỏ CITEXT để tránh lỗi phân quyền trên Cloud.
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Nguyên tắc tử huyệt: Không bao giờ lưu mật khẩu gốc (plaintext). Cột này lưu chuỗi đã mã hóa một chiều (Bcrypt/Argon2).
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    public UserRole Role { get; set; } = UserRole.user;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiry { get; set; }

    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual UserStorage? UserStorage { get; set; }
}
