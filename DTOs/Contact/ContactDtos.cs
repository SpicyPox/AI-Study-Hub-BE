using System.ComponentModel.DataAnnotations;

namespace AIStudyHub.Api.DTOs.Contact;

public record CreateContactMessageRequest(
    [Required, MaxLength(150)] string Name,
    [Required, EmailAddress, MaxLength(255)] string Email,
    [Required, MaxLength(255)] string Subject,
    [Required, MaxLength(5000)] string Message
);

public record ContactMessageDto(
    Guid Id, string Name, string Email, string Subject, string Message,
    bool IsRead, DateTime CreatedAt
);

public record ContactMessageListResponse(IEnumerable<ContactMessageDto> Messages, int Total, int UnreadCount);
