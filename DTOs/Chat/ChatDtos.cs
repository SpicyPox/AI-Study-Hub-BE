using System.ComponentModel.DataAnnotations;

namespace AIStudyHub.Api.DTOs.Chat;

public class CreateConversationRequest
{
    public string? Title { get; set; }
    public Guid? DocumentId { get; set; }
}

public record SendMessageRequest(
    [Required] string Content, 
    Guid? DocumentId
);

public record UpdateConversationRequest(string? Title, bool? IsPinned);

public record ConversationDto(Guid Id, string Title, Guid? DocumentId, DateTime UpdatedAt, bool IsPinned);
public record MessageDto(Guid Id, string Role, string Content, int TokensUsed, DateTime CreatedAt);
public record ConversationListResponse(IEnumerable<ConversationDto> Conversations);
public record MessageListResponse(IEnumerable<MessageDto> Messages);
