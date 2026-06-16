namespace AIStudyHub.Api.DTOs.Chat;

public record CreateConversationRequest(string? Title, Guid? DocumentId);
public record SendMessageRequest(string Content, Guid? DocumentId);

public record ConversationDto(Guid Id, string Title, Guid? DocumentId, DateTime UpdatedAt);
public record MessageDto(Guid Id, string Role, string Content, int TokensUsed, DateTime CreatedAt);
public record ConversationListResponse(IEnumerable<ConversationDto> Conversations);
public record MessageListResponse(IEnumerable<MessageDto> Messages);
