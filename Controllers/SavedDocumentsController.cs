using System.Security.Claims;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Documents;
using AIStudyHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/saved-documents")]
[Authorize]
public class SavedDocumentsController(AppDbContext db) : ControllerBase
{
    [HttpPost("{documentId:guid}")]
    public async Task<IActionResult> Save(Guid documentId)
    {
        var uid = UserId();
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted)
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (doc.Visibility != DocVisibility.@public && doc.UserId != uid)
            throw new KeyNotFoundException("Tai lieu khong ton tai.");

        // PK composite (UserId, DocumentId) da chong luu trung - chi can kiem tra truoc de tranh
        // loi vi pham unique constraint khi nguoi dung bam luu 2 lan lien tiep.
        var already = await db.Favorites.AnyAsync(f => f.UserId == uid && f.DocumentId == documentId);
        if (!already)
        {
            db.Favorites.Add(new Favorite { UserId = uid, DocumentId = documentId });
            await db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Unsave(Guid documentId)
    {
        var uid = UserId();
        var fav = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == uid && f.DocumentId == documentId);
        if (fav != null)
        {
            db.Favorites.Remove(fav);
            await db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpGet("my")]
    public async Task<DocumentListResponse> GetMy()
    {
        var uid = UserId();
        var docs = await db.Favorites
            .Where(f => f.UserId == uid)
            .Include(f => f.Document).ThenInclude(d => d.Subject)
            .Include(f => f.Document).ThenInclude(d => d.User)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.Document)
            .Where(d => !d.IsDeleted)
            .ToListAsync();

        return new DocumentListResponse(docs.Select(ToDto), docs.Count);
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
