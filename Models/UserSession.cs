using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

/// <summary>
/// Một phiên đăng nhập (1 thiết bị/trình duyệt = 1 dòng). Thay thế cột RefreshToken đơn lẻ trên
/// User trước đây (chỉ cho phép 1 phiên tại một thời điểm) để hỗ trợ đăng nhập song song trên
/// nhiều thiết bị và cho phép thu hồi từng phiên riêng lẻ.
/// </summary>
public partial class UserSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Chỉ lưu SHA-256 hash của refresh token, không lưu token gốc (giống nguyên tắc PasswordHash)
    /// để lộ DB không đồng nghĩa với chiếm được phiên đăng nhập.
    /// </summary>
    public string RefreshTokenHash { get; set; } = null!;

    public string? DeviceName { get; set; }

    public string? UserAgent { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastActiveAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
