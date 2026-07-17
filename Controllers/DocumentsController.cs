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
        [FromQuery] string? scope,
        [FromQuery] string? sort)
    {
        var uid = UserId();
        var isPublicScope = string.Equals(scope, "public", StringComparison.OrdinalIgnoreCase);

        var query = db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Where(d => !d.IsDeleted);

        // scope=public  -> browse public documents shared by everyone (the community hub).
        // default (mine) -> only the current user's own documents.
        // KHONG so sanh d.Visibility == DocVisibility.@public truc tiep trong SQL: EF Core +
        // Npgsql hien tai gui sai kieu tham so cho cot enum native cua Postgres (gui int hoac text
        // deu bi Postgres tu choi voi loi 42883 "operator does not exist"). De an toan, query het
        // tap con can thiet roi loc Visibility o phia C# sau khi da ToListAsync().
        if (!isPublicScope)
            query = query.Where(d => d.UserId == uid);

        if (subjectId.HasValue) query = query.Where(d => d.SubjectId == subjectId);
        if (!string.IsNullOrEmpty(type)) query = query.Where(d => d.FileType == type);
        if (!string.IsNullOrEmpty(q)) query = query.Where(d => d.Title.ToLower().Contains(q.ToLower()));

        var docs = await query.OrderByDescending(d => d.UpdatedAt).ToListAsync();
        if (isPublicScope) docs = docs.Where(d => d.Visibility == DocVisibility.@public).ToList();

        var (avgMap, countMap, myMap) = await LoadRatingsAsync(docs.Select(d => d.Id), uid);
        var dtos = docs.Select(d => ToDto(d, avgMap, countMap, myMap));

        // sort=rating -> dùng cho khu "Top tài liệu được đánh giá cao" ở trang Tổng quan.
        if (string.Equals(sort, "rating", StringComparison.OrdinalIgnoreCase))
            dtos = dtos.OrderByDescending(d => d.AverageRating).ThenByDescending(d => d.RatingCount);

        var list = dtos.ToList();
        return new DocumentListResponse(list, list.Count);
    }

    [HttpGet("{id:guid}")]
    public async Task<DocumentDto> GetById(Guid id)
    {
        var uid = UserIdOrNull();
        var doc = await db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (doc.Visibility != DocVisibility.@public && doc.UserId != uid)
            throw new KeyNotFoundException("Tai lieu khong ton tai.");

        var (avgMap, countMap, myMap) = await LoadRatingsAsync([id], uid);
        return ToDto(doc, avgMap, countMap, myMap);
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

        return await ToDtoAsync(doc);
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
        return await ToDtoAsync(doc);
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
        return await ToDtoAsync(doc);
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

    // Khong [Authorize]: khach/chua dang nhap van tai duoc tai lieu public (R19).
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

    // File office (doc/docx/xls/xlsx/ppt/pptx) khong xem duoc truc tiep trong trinh duyet, nen
    // server dung Microsoft Office Online Viewer (dich vu mien phi cua Microsoft) de nhung qua
    // iframe, tranh phai tu convert file phia backend. PDF/anh thi browser tu doc duoc, tra ve
    // luon URL Cloudinary goc.
    private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase) { "png", "jpg", "jpeg", "gif", "webp" };
    private static readonly HashSet<string> OfficeTypes = new(StringComparer.OrdinalIgnoreCase) { "doc", "docx", "xls", "xlsx", "ppt", "pptx" };

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id)
    {
        var uid = UserIdOrNull();
        var doc = await db.Documents.Include(d => d.CloudFile)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted
                && (d.Visibility == DocVisibility.@public || d.UserId == uid))
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (doc.CloudFile == null || string.IsNullOrEmpty(doc.CloudFile.CloudUrl))
            return BadRequest("File khong ton tai tren cloud.");

        var fileUrl = doc.CloudFile.CloudUrl;
        var ext = doc.FileType ?? "";

        string mode;
        string previewUrl;
        if (ImageTypes.Contains(ext))
        {
            mode = "image";
            previewUrl = fileUrl;
        }
        else if (string.Equals(ext, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            mode = "pdf";
            previewUrl = fileUrl;
        }
        else if (OfficeTypes.Contains(ext))
        {
            mode = "office";
            previewUrl = $"https://view.officeapps.live.com/op/view.aspx?src={Uri.EscapeDataString(fileUrl)}";
        }
        else
        {
            mode = "unsupported";
            previewUrl = fileUrl;
        }

        return Ok(new { mode, url = previewUrl, fileUrl });
    }

    [Authorize]
    [HttpPost("{id:guid}/summarize")]
    public async Task<IActionResult> Summarize(Guid id, CancellationToken ct)
    {
        var uid = UserId();
        var doc = await db.Documents
            .Include(d => d.CloudFile)
            .FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (doc.Visibility != DocVisibility.@public && doc.UserId != uid)
            throw new KeyNotFoundException("Tai lieu khong ton tai.");

        var text = await extractor.ExtractTextAsync(doc, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("Không thể trích xuất nội dung văn bản từ tài liệu này để tóm tắt.");
        }

        var summary = await gemini.GenerateSummaryAsync(text, ct);
        return Ok(new { summary });
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

    private async Task<(Dictionary<Guid, double> Avg, Dictionary<Guid, int> Count, Dictionary<Guid, int> Mine)> LoadRatingsAsync(IEnumerable<Guid> docIds, Guid? uid)
    {
        var ids = docIds.ToList();
        var agg = await db.DocumentRatings
            .Where(r => ids.Contains(r.DocumentId))
            .GroupBy(r => r.DocumentId)
            .Select(g => new { DocumentId = g.Key, Avg = g.Average(r => (double)r.Stars), Count = g.Count() })
            .ToListAsync();

        var avgMap = agg.ToDictionary(x => x.DocumentId, x => Math.Round(x.Avg, 1));
        var countMap = agg.ToDictionary(x => x.DocumentId, x => x.Count);

        var mineMap = new Dictionary<Guid, int>();
        if (uid.HasValue)
        {
            mineMap = await db.DocumentRatings
                .Where(r => r.UserId == uid.Value && ids.Contains(r.DocumentId))
                .ToDictionaryAsync(r => r.DocumentId, r => r.Stars);
        }

        return (avgMap, countMap, mineMap);
    }

    private async Task<DocumentDto> ToDtoAsync(Document d)
    {
        var (avg, count, mine) = await LoadRatingsAsync([d.Id], UserIdOrNull());
        return ToDto(d, avg, count, mine);
    }

    private static DocumentDto ToDto(Document d, Dictionary<Guid, double> avgMap, Dictionary<Guid, int> countMap, Dictionary<Guid, int> mineMap)
    {
        return new DocumentDto(
            d.Id, d.Title, d.FileType ?? string.Empty, FormatSize(d.FileSize ?? 0),
            d.Description, Array.Empty<string>(), 0,
            d.Visibility == DocVisibility.@public, null, null,
            d.SubjectId, d.Subject?.Name, null, d.Subject?.Code,
            d.User?.Username ?? string.Empty, d.UpdatedAt, d.CreatedAt,
            avgMap.GetValueOrDefault(d.Id), countMap.GetValueOrDefault(d.Id),
            mineMap.TryGetValue(d.Id, out var mine) ? mine : null
        );
    }

    // ─── Đánh giá sao & bình luận cộng đồng ─────────────────────────────────────
    [HttpGet("{id:guid}/ratings")]
    public async Task<RatingSummaryDto> GetRatings(Guid id)
    {
        var uid = UserIdOrNull();
        var ratings = await db.DocumentRatings.Where(r => r.DocumentId == id).ToListAsync();
        var avg = ratings.Count > 0 ? Math.Round(ratings.Average(r => r.Stars), 1) : 0;
        var mine = uid.HasValue ? ratings.FirstOrDefault(r => r.UserId == uid.Value)?.Stars : null;
        return new RatingSummaryDto(avg, ratings.Count, mine);
    }

    [Authorize]
    [HttpPost("{id:guid}/ratings")]
    public async Task<RatingSummaryDto> RateDocument(Guid id, RateDocumentRequest req)
    {
        var uid = UserId();
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted
            && (d.Visibility == DocVisibility.@public || d.UserId == uid))
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (doc.UserId == uid)
            throw new InvalidOperationException("Bạn không thể tự đánh giá tài liệu của chính mình.");

        var existing = await db.DocumentRatings.FirstOrDefaultAsync(r => r.UserId == uid && r.DocumentId == id);
        if (existing != null)
        {
            existing.Stars = req.Stars;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.DocumentRatings.Add(new DocumentRating { UserId = uid, DocumentId = id, Stars = req.Stars });
        }
        await db.SaveChangesAsync();

        var ratings = await db.DocumentRatings.Where(r => r.DocumentId == id).ToListAsync();
        var avg = Math.Round(ratings.Average(r => r.Stars), 1);
        return new RatingSummaryDto(avg, ratings.Count, req.Stars);
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<CommentListResponse> GetComments(Guid id)
    {
        var uid = UserIdOrNull();
        var comments = await db.DocumentComments
            .Include(c => c.User)
            .Where(c => c.DocumentId == id)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var dtos = comments.Select(c => new CommentDto(
            c.Id, c.Content, c.User.Username, c.UserId, c.CreatedAt,
            uid.HasValue && c.UserId == uid.Value
        ));
        return new CommentListResponse(dtos, comments.Count);
    }

    [Authorize]
    [HttpPost("{id:guid}/comments")]
    public async Task<CommentDto> AddComment(Guid id, CreateCommentRequest req)
    {
        var uid = UserId();
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted
            && (d.Visibility == DocVisibility.@public || d.UserId == uid))
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        var user = await db.Users.FindAsync(uid) ?? throw new KeyNotFoundException("Nguoi dung khong ton tai.");
        var comment = new DocumentComment { UserId = uid, DocumentId = doc.Id, Content = req.Content.Trim() };
        db.DocumentComments.Add(comment);
        await db.SaveChangesAsync();

        return new CommentDto(comment.Id, comment.Content, user.Username, uid, comment.CreatedAt, true);
    }

    [Authorize]
    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
    {
        var uid = UserId();
        var comment = await db.DocumentComments.FirstOrDefaultAsync(c => c.Id == commentId && c.DocumentId == id)
            ?? throw new KeyNotFoundException("Binh luan khong ton tai.");

        if (comment.UserId != uid)
            throw new UnauthorizedAccessException("Bạn không có quyền xoá bình luận này.");

        db.DocumentComments.Remove(comment);
        await db.SaveChangesAsync();
        return Ok();
    }
}
