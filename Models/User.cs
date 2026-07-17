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

    public Guid? RoleId { get; set; }

    public virtual Role? Role { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Đã bật xác thực 2 bước (TOTP) hay chưa.</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>Secret TOTP (mã hoá AES trước khi lưu) một khi đã BẬT 2FA thành công.</summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>Secret tạm sinh ra khi người dùng bấm "Bật 2FA" nhưng chưa nhập mã xác nhận lần đầu.</summary>
    public string? TwoFactorPendingSecret { get; set; }

    public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();

    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<DocumentRating> DocumentRatings { get; set; } = new List<DocumentRating>();

    public virtual ICollection<DocumentComment> DocumentComments { get; set; } = new List<DocumentComment>();

    public virtual ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();

    public virtual UserStorage? UserStorage { get; set; }
}
