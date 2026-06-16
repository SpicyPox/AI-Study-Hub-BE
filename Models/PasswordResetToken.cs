using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

/// <summary>
/// Quản lý token quên mật khẩu. Có cột expires_at (hạn dùng) và is_used (đã dùng) để vô hiệu hóa link cũ, chống hack.
/// </summary>
public partial class PasswordResetToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = null!;

    public bool IsUsed { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
