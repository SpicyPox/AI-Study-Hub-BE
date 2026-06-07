using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

/// <summary>
/// Trái tim của hệ thống. Áp dụng cơ chế ON DELETE CASCADE từ bảng Users: Xóa user là tự động quét sạch tài liệu, không lo rác DB.
/// </summary>
public partial class Document
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? SubjectId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? FilePath { get; set; }

    public string? FileType { get; set; }

    /// <summary>
    /// Bắt buộc dùng BIGINT để lưu số Bytes. Nếu dùng INT bình thường, file &gt;2GB sẽ bị tràn bộ nhớ (overflow) gây sập hệ thống.
    /// </summary>
    public long? FileSize { get; set; }

    public DocVisibility Visibility { get; set; } = DocVisibility.@public;

    /// <summary>
    /// Cờ Xóa mềm (Soft Delete). Đổi thành TRUE thì file chui vào thùng rác, giữ lại được 30 ngày để khôi phục thay vì bốc hơi vĩnh viễn.
    /// </summary>
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual CloudFile? CloudFile { get; set; }

    public virtual Subject? Subject { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<ChatSession> Sessions { get; set; } = new List<ChatSession>();

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
