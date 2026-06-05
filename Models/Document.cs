namespace AIStudyHub.Api.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public int? Pages { get; set; }
    public bool IsPublic { get; set; }
    public string? ShareToken { get; set; }
    public Guid UserId { get; set; }
    public Guid? SubjectId { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Subject? Subject { get; set; }
}
