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
            .Where(d => d.UserId == uid && d.IsConfirmed);

        if (subjectId.HasValue) query = query.Where(d => d.SubjectId == subjectId);
        if (!string.IsNullOrEmpty(type)) query = query.Where(d => d.Type == type);
        if (!string.IsNullOrEmpty(q)) query = query.Where(d => d.Name.ToLower().Contains(q.ToLower()));

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
            .FirstOrDefaultAsync(d => d.Id == id && (d.IsPublic || d.UserId == uid))
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
            Name = req.FileName,
            Type = Path.GetExtension(req.FileName).TrimStart('.').ToLower(),
            S3Key = s3Key,
            UserId = uid,
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
        doc.Tags = req.Tags;
        doc.IsPublic = req.IsPublic;
        doc.Description = req.Description;
        doc.IsConfirmed = true;
        doc.UpdatedAt = DateTime.UtcNow;

        if (req.IsPublic && doc.ShareToken is null)
            doc.ShareToken = Guid.NewGuid().ToString("N");

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

        if (req.Name is not null) doc.Name = req.Name;
        if (req.Tags is not null) doc.Tags = req.Tags;
        if (req.SubjectId.HasValue) doc.SubjectId = req.SubjectId;
        if (req.IsPublic.HasValue) doc.IsPublic = req.IsPublic.Value;
        doc.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(doc);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId())
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại.");
        await s3.DeleteObjectAsync(doc.S3Key);
        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("share/{shareToken}")]
    public async Task<DocumentDto> GetShared(string shareToken)
    {
        var doc = await db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.ShareToken == shareToken && d.IsPublic)
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại hoặc không công khai.");
        return ToDto(doc);
    }

    [Authorize]
    [HttpPost("{id:guid}/share")]
    public async Task<ShareResponse> CreateShare(Guid id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId())
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại.");

        doc.ShareToken ??= Guid.NewGuid().ToString("N");
        doc.IsPublic = true;
        doc.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var baseUrl = config["App:BaseUrl"] ?? "http://localhost:5173";
        return new ShareResponse(doc.ShareToken, $"{baseUrl}/share/{doc.ShareToken}");
    }

    [Authorize]
    [HttpDelete("{id:guid}/share")]
    public async Task<IActionResult> RevokeShare(Guid id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId())
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại.");
        doc.IsPublic = false;
        doc.ShareToken = null;
        doc.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
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
            d.Id, d.Name, d.Type, FormatSize(d.SizeBytes),
            d.Description, d.Tags, d.Pages,
            d.IsPublic, d.ShareToken,
            d.ShareToken is not null ? $"{baseUrl}/share/{d.ShareToken}" : null,
            d.SubjectId, d.Subject?.Name, d.Subject?.Color, d.Subject?.Code,
            d.User?.Name ?? string.Empty, d.UpdatedAt, d.CreatedAt
        );
    }
}
