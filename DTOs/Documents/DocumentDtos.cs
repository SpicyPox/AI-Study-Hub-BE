using System.ComponentModel.DataAnnotations;

namespace AIStudyHub.Api.DTOs.Documents;

public class UploadDocumentRequest
{
    [Required]
    public IFormFile? File { get; set; }
    public Guid? SubjectId { get; set; }
    public bool IsPublic { get; set; }
    public string? Description { get; set; }
    // Chuoi tags cach nhau boi dau phay (form-data khong ho tro mang gon nhu JSON).
    public string? Tags { get; set; }
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
    string OwnerName, DateTime UpdatedAt, DateTime CreatedAt,
    double AverageRating = 0, int RatingCount = 0, int? MyRating = null
);

public record DocumentListResponse(IEnumerable<DocumentDto> Documents, int Total);
public record ShareResponse(string ShareToken, string ShareUrl);

// ─── Đánh giá sao & bình luận cộng đồng ─────────────────────────────────────
public record RateDocumentRequest(
    [Required, Range(1, 5)] int Stars
);

public record RatingSummaryDto(double AverageRating, int RatingCount, int? MyRating);

public record CreateCommentRequest(
    [Required, MinLength(1), MaxLength(1000)] string Content
);

public record CommentDto(
    Guid Id, string Content, string AuthorName, Guid AuthorId,
    DateTime CreatedAt, bool IsMine
);

public record CommentListResponse(IEnumerable<CommentDto> Comments, int Total);
