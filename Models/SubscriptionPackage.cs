using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

public partial class SubscriptionPackage
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int DurationDays { get; set; }

    public int AiChatLimit { get; set; }

    public long BaseStorageBytes { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
