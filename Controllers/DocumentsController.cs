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
public class DocumentsController(AppDbContext db, S3Service s3, IConfiguration config) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<DocumentListResponse> GetAll(
        [FromQuery] Guid? subjectId,
        [FromQuery] string? type,
        [FromQuery] string? q)
    {
        var uid = UserId();
        var query = db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Where(d => d.UserId == uid && !d.IsDeleted);

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
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại.");
        return ToDto(doc);
    }

    [Authorize]
    [HttpPost("upload-url")]
    public async Task<UploadUrlResponse> GetUploadUrl(GetUploadUrlRequest req)
    {
        var uid = UserId();
        var s3Key = s3.BuildS3Key(uid, req.FileName);
        var doc = new Document
        {
            Title = req.FileName,
            FileType = Path.GetExtension(req.FileName).TrimStart('.').ToLower(),
            UserId = uid,
            CloudFile = new CloudFile { CloudKey = s3Key, CloudUrl = "", Provider = "s3" }
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var url = s3.GetPresignedUploadUrl(s3Key, req.FileType);
        return new UploadUrlResponse(url, doc.Id);
    }

    [Authorize]
    [HttpPost("{id:guid}/confirm")]
    public async Task<DocumentDto> Confirm(Guid id, ConfirmUploadRequest req)
    {
        var doc = await db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId())
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại.");

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
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại.");

        if (req.Name is not null) doc.Title = req.Name;
        if (req.SubjectId.HasValue) doc.SubjectId = req.SubjectId;
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
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại.");
        if (doc.CloudFile != null) await s3.DeleteObjectAsync(doc.CloudFile.CloudKey);
        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        return Ok();
    }

    [Authorize]
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var doc = await db.Documents.Include(d => d.CloudFile).FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId())
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại.");

        if (doc.CloudFile == null || string.IsNullOrEmpty(doc.CloudFile.CloudKey))
            return BadRequest("File không tồn tại trên cloud.");

        var url = s3.GetPresignedDownloadUrl(doc.CloudFile.CloudKey);
        return Ok(new { Url = url });
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
        var baseUrl = config["App:BaseUrl"] ?? "http://localhost:5173";
        return new DocumentDto(
            d.Id, d.Title, d.FileType, FormatSize(d.FileSize ?? 0),
            d.Description, Array.Empty<string>(), 0,
            d.Visibility == DocVisibility.@public, null, null,
            d.SubjectId, d.Subject?.Name, null, d.Subject?.Code,
            d.User?.Username ?? string.Empty, d.UpdatedAt, d.CreatedAt
        );
    }
}
