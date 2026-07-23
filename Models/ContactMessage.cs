using System;

namespace AIStudyHub.Api.Models;

/// <summary>Yêu cầu liên hệ gửi từ trang /contact (công khai, không cần đăng nhập).</summary>
public partial class ContactMessage
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
