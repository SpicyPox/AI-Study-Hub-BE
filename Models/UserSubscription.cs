using System;

namespace AIStudyHub.Api.Models;

public partial class UserSubscription
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PackageId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual SubscriptionPackage Package { get; set; } = null!;
}
