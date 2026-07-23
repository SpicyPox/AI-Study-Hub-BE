using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

public partial class ChatSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? Title { get; set; }

    public bool IsPinned { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
