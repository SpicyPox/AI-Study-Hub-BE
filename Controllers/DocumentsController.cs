using System.Security.Claims;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Documents;
using AIStudyHub.Api.Models;
using AIStudyHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController(AppDbContext db, CloudinaryService cloudinary, DocumentTextExtractor extractor, GeminiService gemini) : ControllerBase
{
    // Maximum allowed upload size per file (50 MB).
    private const long MaxUploadBytes = 50L * 1024 * 1024;

    [Authorize]
    [HttpGet]
    public async Task<DocumentListResponse> GetAll(
        [FromQuery] Guid? subjectId,
        [FromQuery] string? type,
        [FromQuery] string? q,
        [FromQuery] string? scope)
    {
        var uid = UserId();
        var query = db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Where(d => !d.IsDeleted);

        // scope=public  -> browse public documents shared by everyone (the community hub).
        // default (mine) -> only the current user's own documents.
        if (string.Equals(scope, "public", StringComparison.OrdinalIgnoreCase))
            query = query.Where(d => d.Visibility == DocVisibility.@public);
        else
            query = query.Where(d => d.UserId == uid);

        if (subjectId.HasValue) query = query.Where(d => d.SubjectId == subjectId);
        if (!string.IsNullOrEmpty(type)) query = query.Where(d => d.FileType == type);
        if (!string.IsNullOrEmpty(q)) query = query.Where(d => d.Title.ToLower().Contains(q.ToLower()));

        var docs = await query.OrderByDescending(d => d.UpdatedAt).ToListAsync();
        return new DocumentListResponse(docs.Select(ToDto), docs.Count);
    }

    [HttpGet("{id:guid}")]
    public async Task<DocumentDto> GetById(Guid id)
    {
        var uid = UserIdOrNull();
        var doc = await db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id && (d.Visibility == DocVisibility.@public || d.UserId == uid))
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        return ToDto(doc);
    }

    [Authorize]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] UploadDocumentRequest req)
    {
        var file = req.File;
        if (file is null || file.Length == 0)
            return BadRequest("File khong hop le.");

        if (file.Length > MaxUploadBytes)
            return BadRequest("File vuot qua gioi han 50 MB.");

        // Kiểm tra SubjectId có tồn tại không (tránh lỗi FK constraint)
        if (req.SubjectId.HasValue)
        {
            var subjectExists = await db.Subjects.AnyAsync(s => s.Id == req.SubjectId.Value);
            if (!subjectExists)
                return BadRequest($"SubjectId '{req.SubjectId}' không tồn tại. Hãy để trống nếu không muốn gắn môn học.");
        }

        var uid = UserId();
        var upload = await cloudinary.UploadDocumentAsync(file, uid);

        var doc = new Document
        {
            Title = Path.GetFileName(file.FileName),
            Description = req.Description,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower(),
            FileSize = file.Length,
            SubjectId = req.SubjectId,
            UserId = uid,
            Visibility = req.IsPublic ? DocVisibility.@public : DocVisibility.@private,
            CloudFile = new CloudFile
            {
                CloudKey = upload.PublicId,
                CloudUrl = upload.SecureUrl,
                Provider = "cloudinary",
                Status = CloudStatus.uploaded
            }
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        await db.Entry(doc).Reference(d => d.Subject).LoadAsync();
        await db.Entry(doc).Reference(d => d.User).LoadAsync();

        return ToDto(doc);
    }

    [Authorize]
    [HttpPost("{id:guid}/confirm")]
    public async Task<DocumentDto> Confirm(Guid id, ConfirmUploadRequest req)
    {
        var doc = await db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId())
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (req.SubjectId.HasValue && !await db.Subjects.AnyAsync(s => s.Id == req.SubjectId.Value))
            throw new InvalidOperationException($"SubjectId '{req.SubjectId}' không tồn tại.");

        doc.SubjectId = req.SubjectId;
        doc.Visibility = req.IsPublic ? DocVisibility.@public : DocVisibility.@private;
        doc.Description = req.Description;
        doc.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(doc);
    }

    [Authorize]
    [HttpPatch("{id:guid}")]
    public async Task<DocumentDto> Update(Guid id, UpdateDocumentRequest req)
    {
        var doc = await db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId())
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (req.Name is not null) doc.Title = req.Name;
        
        if (req.SubjectId.HasValue) 
        {
            if (!await db.Subjects.AnyAsync(s => s.Id == req.SubjectId.Value))
                throw new InvalidOperationException($"SubjectId '{req.SubjectId}' không tồn tại.");
            doc.SubjectId = req.SubjectId;
        }
        if (req.IsPublic.HasValue) doc.Visibility = req.IsPublic.Value ? DocVisibility.@public : DocVisibility.@private;
        doc.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(doc);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doc = await db.Documents.Include(d => d.CloudFile).FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId())
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (doc.CloudFile != null) await cloudinary.DeleteDocumentAsync(doc.CloudFile.CloudKey);

        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        return Ok();
    }

    [Authorize]
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var uid = UserIdOrNull();
        var doc = await db.Documents.Include(d => d.CloudFile)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted
                && (d.Visibility == DocVisibility.@public || d.UserId == uid))
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (doc.CloudFile == null || string.IsNullOrEmpty(doc.CloudFile.CloudUrl))
            return BadRequest("File khong ton tai tren cloud.");

        return Ok(new { Url = doc.CloudFile.CloudUrl });
    }

    [Authorize]
    [HttpPost("{id:guid}/summarize")]
    public async Task<IActionResult> Summarize(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents
            .Include(d => d.CloudFile)
            .FirstOrDefaultAsync(d => d.Id == id && (d.Visibility == DocVisibility.@public || d.UserId == UserId()), ct)
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        var text = await extractor.ExtractTextAsync(doc, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("Không thể trích xuất nội dung văn bản từ tài liệu này để tóm tắt.");
        }

        var summary = await gemini.GenerateSummaryAsync(text, ct);
        return Ok(new { summary });
    }

    [Authorize]
    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetUploadStatus(Guid id)
    {
        var uid = UserIdOrNull();
        var doc = await db.Documents
            .Include(d => d.CloudFile)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted
                && (d.Visibility == DocVisibility.@public || d.UserId == uid))
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        var status = doc.CloudFile?.Status.ToString() ?? "no_file";
        return Ok(new
        {
            documentId = doc.Id,
            status,
            cloudUrl = doc.CloudFile?.CloudUrl,
            uploadedAt = doc.CloudFile?.UploadedAt
        });
    }

    [Authorize]
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id)
    {
        var uid = UserIdOrNull();
        var doc = await db.Documents
            .Include(d => d.CloudFile)
            .Include(d => d.Subject)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted && (d.Visibility == DocVisibility.@public || d.UserId == uid))
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (doc.CloudFile == null || string.IsNullOrEmpty(doc.CloudFile.CloudUrl))
            return BadRequest("File khong ton tai tren cloud.");

        return Ok(new
        {
            documentId = doc.Id,
            title = doc.Title,
            fileType = doc.FileType,
            fileSize = FormatSize(doc.FileSize ?? 0),
            description = doc.Description,
            previewUrl = doc.CloudFile.CloudUrl,
            subjectName = doc.Subject?.Name,
            ownerName = doc.User?.Username,
            createdAt = doc.CreatedAt,
            updatedAt = doc.UpdatedAt
        });
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Guid? UserIdOrNull()
    {
        var val = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return val is not null ? Guid.Parse(val) : null;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024) return $"{bytes / 1_024.0:F0} KB";
        return $"{bytes} B";
    }

    private DocumentDto ToDto(Document d)
    {
        return new DocumentDto(
            d.Id, d.Title, d.FileType ?? string.Empty, FormatSize(d.FileSize ?? 0),
            d.Description, Array.Empty<string>(), 0,
            d.Visibility == DocVisibility.@public, null, null,
            d.SubjectId, d.Subject?.Name, null, d.Subject?.Code,
            d.User?.Username ?? string.Empty, d.UpdatedAt, d.CreatedAt
        );
    }
}
