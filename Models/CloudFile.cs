using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

/// <summary>
/// Lưu link S3/MinIO thực tế. Tách riêng bảng này để lỡ mai mốt chê AWS đắt, đổi sang Google Cloud thì chỉ sửa ở đây, không ảnh hưởng logic Documents.
/// </summary>
public partial class CloudFile
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public string? Provider { get; set; }

    public string CloudUrl { get; set; } = null!;

    public string CloudKey { get; set; } = null!;

    public CloudStatus Status { get; set; } = CloudStatus.pending;

    public DateTime UploadedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Document Document { get; set; } = null!;
}
