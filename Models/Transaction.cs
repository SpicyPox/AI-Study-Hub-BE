using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

/// <summary>
/// Lưu lịch sử nạp tiền. Chìa khóa của mô hình Mua bao nhiêu dùng bấy nhiêu.
/// </summary>
public partial class Transaction
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? PackageId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// Bí quyết linh hoạt: Khách mua gói 10GB hay nhập tay 3.5GB thì Backend chỉ việc quy ra Bytes ném vào đây. Hóa đơn completed là Trigger số 3 tự bốc số này cộng thẳng vào ví storage.
    /// </summary>
    public long StorageAddedBytes { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.pending;

    public PaymentMethod Method { get; set; }

    public string? TransactionRef { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual StoragePackage? Package { get; set; }

    public virtual User User { get; set; } = null!;
}
