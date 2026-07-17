using System;

namespace AIStudyHub.Api.Models;

/// <summary>
/// Bình luận của user về một tài liệu. Nhiều bình luận / user / tài liệu -> khoá chính riêng (Guid).
/// </summary>
public partial class DocumentComment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid DocumentId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Document Document { get; set; } = null!;
}
