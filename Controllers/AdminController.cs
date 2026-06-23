using System.Security.Claims;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Admin;
using AIStudyHub.Api.DTOs.Documents;
using AIStudyHub.Api.Models;
using AIStudyHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController(AppDbContext db, CloudinaryService cloudinary) : ControllerBase
{
    [HttpGet("users")]
    public async Task<AdminUserListResponse> GetUsers([FromQuery] string? q, [FromQuery] int page = 1)
    {
        var query = db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(q))
            query = query.Where(u => u.Username.ToLower().Contains(q.ToLower()) || u.Email.ToLower().Contains(q.ToLower()));

        var total = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * 20).Take(20)
            .Select(u => new AdminUserDto(
                u.Id, u.Username, u.Email, u.Role != null ? u.Role.Name : "user",
                "active",
                u.Documents.Count, u.Documents.Sum(d => d.FileSize ?? 0),
                0, // u.ChatSessions.SelectMany(c => c.ChatMessages).Sum(m => m.TokensUsed) - missing in DB
                u.CreatedAt))
            .ToListAsync();

        return new AdminUserListResponse(users, total);
    }

    [HttpPatch("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserRequest req)
    {
        var user = await db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("Người dùng không tồn tại.");

        // DB no longer has IsActive
        if (req.Role is not null) 
        {
            var dbRole = await db.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == req.Role.ToLower());
            if (dbRole != null)
            {
                user.RoleId = dbRole.Id;
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPatch("users/{id:guid}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid id, UpdateUserRoleRequest req)
    {
        var user = await db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("Người dùng không tồn tại.");

        var dbRole = await db.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == req.Role.ToLower())
            ?? throw new InvalidOperationException($"Role '{req.Role}' không tồn tại.");

        user.RoleId = dbRole.Id;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        if (id == UserId())
            throw new InvalidOperationException("Không thể tự xoá chính tài khoản admin đang đăng nhập.");

        var user = await db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("Người dùng không tồn tại.");

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("stats")]
    public async Task<AdminStatsDto> GetStats()
    {
        var totalUsers = await db.Users.CountAsync();
        var totalDocs = await db.Documents.CountAsync(d => !d.IsDeleted);
        var totalTokens = 0; // await db.ChatMessages.SumAsync(m => (long)m.TokensUsed);
        return new AdminStatsDto(totalUsers, totalDocs, totalTokens, 0);
    }

    [HttpGet("tokens")]
    public async Task<TokenStatsResponse> GetTokenStats([FromQuery] int days = 14)
    {
        var since = DateTime.UtcNow.AddDays(-days).Date;
        var raw = await db.ChatMessages
            .Where(m => m.CreatedAt >= since)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Tokens = 0 }) // Tokens missing in schema
            .OrderBy(x => x.Date)
            .ToListAsync();

        var daily = Enumerable.Range(0, days)
            .Select(i => since.AddDays(i))
            .Select(d => new TokenDailyDto(
                d.ToString("yyyy-MM-dd"),
                raw.FirstOrDefault(r => r.Date == d)?.Tokens ?? 0))
            .ToList();

        var today = raw.FirstOrDefault(r => r.Date == DateTime.UtcNow.Date)?.Tokens ?? 0;
        var total = 0; // await db.ChatMessages.SumAsync(m => m.TokensUsed);

        return new TokenStatsResponse(daily, today, total);
    }

    // Admin xem TOAN BO tai lieu (ke ca private cua moi user) - khac voi /api/documents
    // (ben do chi tra tai lieu cua chinh nguoi goi hoac tai lieu public).
    [HttpGet("documents")]
    public async Task<DocumentListResponse> GetDocuments([FromQuery] string? q, [FromQuery] int page = 1)
    {
        var query = db.Documents
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Where(d => !d.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(d => d.Title.ToLower().Contains(q.ToLower()));

        var total = await query.CountAsync();
        var docs = await query
            .OrderByDescending(d => d.UpdatedAt)
            .Skip((page - 1) * 20).Take(20)
            .ToListAsync();

        return new DocumentListResponse(docs.Select(ToDocumentDto), total);
    }

    // Admin an/hien tai lieu public cua nguoi dung: chuyen Visibility ve private (Hide=true)
    // hoac tra lai public (Hide=false). Khong them cot moi - dung lai field Visibility da co.
    [HttpPatch("documents/{id:guid}/hide")]
    public async Task<IActionResult> HideDocument(Guid id, HideDocumentRequest req)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted)
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        doc.Visibility = req.Hide ? DocVisibility.@private : DocVisibility.@public;
        doc.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var doc = await db.Documents.Include(d => d.CloudFile).FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Tai lieu khong ton tai.");

        if (doc.CloudFile != null) await cloudinary.DeleteDocumentAsync(doc.CloudFile.CloudKey);

        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        return Ok();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024) return $"{bytes / 1_024.0:F0} KB";
        return $"{bytes} B";
    }

    private static DocumentDto ToDocumentDto(Document d)
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
