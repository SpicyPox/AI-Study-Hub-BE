using System.Security.Claims;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController(AppDbContext db) : ControllerBase
{
    [HttpGet("users")]
    public async Task<AdminUserListResponse> GetUsers([FromQuery] string? q, [FromQuery] int page = 1)
    {
        var query = db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(q))
            query = query.Where(u => u.Name.ToLower().Contains(q.ToLower()) || u.Email.ToLower().Contains(q.ToLower()));

        var total = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * 20).Take(20)
            .Select(u => new AdminUserDto(
                u.Id, u.Name, u.Email, u.Role,
                u.IsActive ? "active" : "suspended",
                u.Documents.Count, u.Documents.Sum(d => d.SizeBytes),
                u.Conversations.SelectMany(c => c.Messages).Sum(m => m.TokensUsed),
                u.CreatedAt))
            .ToListAsync();

        return new AdminUserListResponse(users, total);
    }

    [HttpPatch("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserRequest req)
    {
        var user = await db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("Người dùng không tồn tại.");

        if (req.Status is "active") user.IsActive = true;
        else if (req.Status is "suspended") user.IsActive = false;
        if (req.Role is not null) user.Role = req.Role;

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("stats")]
    public async Task<AdminStatsDto> GetStats()
    {
        var totalUsers = await db.Users.CountAsync();
        var totalDocs = await db.Documents.CountAsync(d => d.IsConfirmed);
        var totalTokens = await db.Messages.SumAsync(m => (long)m.TokensUsed);
        return new AdminStatsDto(totalUsers, totalDocs, totalTokens, 0);
    }

    [HttpGet("tokens")]
    public async Task<TokenStatsResponse> GetTokenStats([FromQuery] int days = 14)
    {
        var since = DateTime.UtcNow.AddDays(-days).Date;
        var raw = await db.Messages
            .Where(m => m.CreatedAt >= since)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Tokens = g.Sum(m => m.TokensUsed) })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var daily = Enumerable.Range(0, days)
            .Select(i => since.AddDays(i))
            .Select(d => new TokenDailyDto(
                d.ToString("yyyy-MM-dd"),
                raw.FirstOrDefault(r => r.Date == d)?.Tokens ?? 0))
            .ToList();

        var today = raw.FirstOrDefault(r => r.Date == DateTime.UtcNow.Date)?.Tokens ?? 0;
        var total = await db.Messages.SumAsync(m => m.TokensUsed);

        return new TokenStatsResponse(daily, today, total);
    }
}
