using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

public partial class ChatMessage
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public ChatRole Role { get; set; }

    public string Content { get; set; } = null!;

    /// <summary>So token AI dung cho tin nhan nay (tong prompt + response tu Gemini usageMetadata).</summary>
    public int TokensUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ChatSession Session { get; set; } = null!;
}
