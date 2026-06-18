using System.ComponentModel.DataAnnotations;

namespace AIStudyHub.Api.DTOs.Documents;

public class UploadDocumentRequest
{
    [Required]
    public IFormFile? File { get; set; }
    public Guid? SubjectId { get; set; }
    public bool IsPublic { get; set; }
    public string? Description { get; set; }
}

public record ConfirmUploadRequest(
    Guid? SubjectId, 
    string[]? Tags, 
    bool IsPublic, 
    string? Description
);

public record UpdateDocumentRequest(
    string? Name, 
    string[]? Tags, 
    Guid? SubjectId, 
    bool? IsPublic
);

public record DocumentDto(
    Guid Id, string Name, string Type, string Size,
    string? Description, string[] Tags, int? Pages,
    bool IsPublic, string? ShareToken, string? ShareUrl,
    Guid? SubjectId, string? SubjectName, string? SubjectColor, string? SubjectCode,
    string OwnerName, DateTime UpdatedAt, DateTime CreatedAt
);

public record DocumentListResponse(IEnumerable<DocumentDto> Documents, int Total);
public record ShareResponse(string ShareToken, string ShareUrl);
