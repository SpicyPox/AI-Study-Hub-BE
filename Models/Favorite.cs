using System;

namespace AIStudyHub.Api.Models;

public partial class Favorite
{
    public Guid UserId { get; set; }

    public Guid DocumentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Document Document { get; set; } = null!;
}
