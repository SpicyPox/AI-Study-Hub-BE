namespace AIStudyHub.Api.Models;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Cuộc trò chuyện mới";
    public Guid UserId { get; set; }
    public Guid? DocumentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = [];
}

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid ConversationId { get; set; }
    public int TokensUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Conversation Conversation { get; set; } = null!;
}
