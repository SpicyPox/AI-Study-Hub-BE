using System;

namespace AIStudyHub.Api.Models;

/// <summary>
/// Đánh giá sao (1-5) của một user cho một tài liệu. Mỗi user chỉ có 1 đánh giá / tài liệu
/// (khoá chính ghép UserId+DocumentId, giống Favorite) — bấm lại là ghi đè (upsert).
/// </summary>
public partial class DocumentRating
{
    public Guid UserId { get; set; }

    public Guid DocumentId { get; set; }

    public int Stars { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Document Document { get; set; } = null!;
}
