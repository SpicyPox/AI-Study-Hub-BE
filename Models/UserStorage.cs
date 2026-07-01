using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

/// <summary>
/// Ví dung lượng. Lưu hoàn toàn bằng Bytes để không bị sai số làm tròn. Backend không cần tính toán gì vì Trigger cân hết.
/// </summary>
public partial class UserStorage
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Tổng dung lượng user có. Khi đăng ký, Trigger số 2 tự động tạo dòng này và nạp sẵn 10MB (10485760 Bytes) làm Free tier.
    /// </summary>
    public long TotalCapacityBytes { get; set; }

    /// <summary>
    /// Dung lượng đã xài. Khi user up file hoặc xóa file (kể cả xóa mềm), Trigger số 4 tự động lấy file_size cộng/trừ vào đây ngay lập tức.
    /// </summary>
    public long UsedBytes { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
