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
                u.ChatSessions.SelectMany(c => c.ChatMessages).Sum(m => m.TokensUsed),
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
        var totalTokens = await db.ChatMessages.SumAsync(m => (long)m.TokensUsed);
        var mrr = await ComputeMrrAsync();
        return new AdminStatsDto(totalUsers, totalDocs, totalTokens, mrr);
    }

    [HttpGet("tokens")]
    public async Task<TokenStatsResponse> GetTokenStats([FromQuery] int days = 14)
    {
        // Cua so `days` ngay ket thuc HOM NAY (gom ca hom nay).
        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var raw = await db.ChatMessages
            .Where(m => m.CreatedAt >= since)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Tokens = g.Sum(x => x.TokensUsed) })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var daily = Enumerable.Range(0, days)
            .Select(i => since.AddDays(i))
            .Select(d => new TokenDailyDto(
                d.ToString("yyyy-MM-dd"),
                raw.FirstOrDefault(r => r.Date == d)?.Tokens ?? 0))
            .ToList();

        var today = raw.FirstOrDefault(r => r.Date == DateTime.UtcNow.Date)?.Tokens ?? 0;
        var total = await db.ChatMessages.SumAsync(m => m.TokensUsed);

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

    // ── Subscriptions: lay tu user_subscriptions + subscription_packages + users ──
    [HttpGet("subscriptions")]
    public async Task<AdminSubscriptionsResponse> GetSubscriptions()
    {
        var now = DateTime.UtcNow;
        var subs = await db.UserSubscriptions
            .OrderByDescending(s => s.StartDate)
            .Select(s => new AdminSubscriptionDto(
                s.Id, s.User.Username, s.User.Email, s.Package.Name,
                s.Package.Price, s.Status, s.StartDate, s.EndDate))
            .ToListAsync();

        var active = subs.Count(s => s.Status == "active" && s.EndDate >= now);
        var pastDue = subs.Count(s => s.EndDate < now || s.Status == "past_due");
        var breakdown = subs.GroupBy(s => s.Plan).ToDictionary(g => g.Key, g => g.Count());
        var mrr = await ComputeMrrAsync();

        return new AdminSubscriptionsResponse(subs, mrr, active, pastDue, breakdown);
    }

    // ── Revenue: doanh thu tu Transactions completed, gom theo quy va theo thang ──
    [HttpGet("revenue")]
    public async Task<AdminRevenueResponse> GetRevenue([FromQuery] int? year)
    {
        var completed = db.Transactions.Where(t => t.Status == PaymentStatus.completed);

        // Cac nam co giao dich hoan tat (moi nhat truoc).
        var years = await completed
            .Select(t => t.CreatedAt.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();

        var selectedYear = year ?? (years.Count > 0 ? years[0] : DateTime.UtcNow.Year);

        // Gom doanh thu theo thang trong nam da chon.
        var monthlyRaw = await completed
            .Where(t => t.CreatedAt.Year == selectedYear)
            .GroupBy(t => t.CreatedAt.Month)
            .Select(g => new { Month = g.Key, Revenue = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync();

        var months = Enumerable.Range(1, 12)
            .Select(m =>
            {
                var r = monthlyRaw.FirstOrDefault(x => x.Month == m);
                return new RevenuePeriodDto($"Th{m:00}", r?.Revenue ?? 0m, r?.Count ?? 0);
            })
            .ToList();

        // Quy = 3 thang: Q1 (1-3), Q2 (4-6), Q3 (7-9), Q4 (10-12).
        var quarters = Enumerable.Range(1, 4)
            .Select(q =>
            {
                var inQ = monthlyRaw.Where(x => (x.Month - 1) / 3 + 1 == q).ToList();
                return new RevenuePeriodDto($"Q{q}", inQ.Sum(x => x.Revenue), inQ.Sum(x => x.Count));
            })
            .ToList();

        var totalYear = monthlyRaw.Sum(x => x.Revenue);
        var txnsYear = monthlyRaw.Sum(x => x.Count);

        // Bao dam nam dang chon luon co trong danh sach de FE render dropdown.
        if (!years.Contains(selectedYear)) years.Insert(0, selectedYear);

        return new AdminRevenueResponse(selectedYear, totalYear, txnsYear, quarters, months, years);
    }

    // ── Revenue summary: so sanh thang nay/nam nay voi ky truoc (MoM & YoY) ──
    [HttpGet("revenue/summary")]
    public async Task<RevenueSummaryResponse> GetRevenueSummary()
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = thisMonthStart.AddMonths(1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var thisYearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextYearStart = thisYearStart.AddYears(1);
        var lastYearStart = thisYearStart.AddYears(-1);

        async Task<RevenuePeriodDto> Agg(string label, DateTime start, DateTime end)
        {
            var q = db.Transactions.Where(t =>
                t.Status == PaymentStatus.completed && t.CreatedAt >= start && t.CreatedAt < end);
            var rev = await q.SumAsync(t => (decimal?)t.Amount) ?? 0m;
            var cnt = await q.CountAsync();
            return new RevenuePeriodDto(label, rev, cnt);
        }

        var thisMonth = await Agg(thisMonthStart.ToString("MM/yyyy"), thisMonthStart, nextMonthStart);
        var lastMonth = await Agg(lastMonthStart.ToString("MM/yyyy"), lastMonthStart, thisMonthStart);
        var thisYear = await Agg(thisYearStart.ToString("yyyy"), thisYearStart, nextYearStart);
        var lastYear = await Agg(lastYearStart.ToString("yyyy"), lastYearStart, thisYearStart);

        static double? Growth(decimal cur, decimal prev) =>
            prev == 0 ? (double?)null : (double)Math.Round((cur - prev) / prev * 100m, 1);

        return new RevenueSummaryResponse(
            thisMonth, lastMonth, Growth(thisMonth.Revenue, lastMonth.Revenue),
            thisYear, lastYear, Growth(thisYear.Revenue, lastYear.Revenue));
    }

    // ── Revenue theo 1 ngay cu the: tong + danh sach giao dich hoan tat trong ngay ──
    [HttpGet("revenue/day")]
    public async Task<RevenueDayResponse> GetRevenueByDay([FromQuery] string? date)
    {
        var day = DateTime.TryParse(date, out var parsed)
            ? DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc)
            : DateTime.UtcNow.Date;
        var end = day.AddDays(1);

        // Lay raw roi format o memory (tranh EF dich ToString/enum sang SQL).
        var raw = await db.Transactions
            .Where(t => t.Status == PaymentStatus.completed && t.CreatedAt >= day && t.CreatedAt < end)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.CreatedAt,
                User = t.User.Username,
                t.PurchaseKind,
                SubName = t.SubscriptionPackage != null ? t.SubscriptionPackage.Name : null,
                t.Method,
                t.Amount
            })
            .ToListAsync();

        var items = raw.Select(r => new RevenueTxnDto(
            r.CreatedAt.ToString("HH:mm"),
            r.User,
            r.PurchaseKind == PurchaseType.subscription_package ? (r.SubName ?? "Gói đăng ký") : "Nạp dung lượng",
            r.Method.ToString(),
            r.Amount)).ToList();

        return new RevenueDayResponse(day.ToString("yyyy-MM-dd"), items.Sum(i => i.Amount), items.Count, items);
    }

    // ── Usage: upload (documents) + chat (chat_messages) theo ngay + hoat dong gan day ──
    [HttpGet("usage")]
    public async Task<AdminUsageResponse> GetUsage([FromQuery] int days = 14)
    {
        // Cua so `days` ngay ket thuc HOM NAY (gom ca hom nay).
        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var uploadsRaw = await db.Documents
            .Where(d => !d.IsDeleted && d.CreatedAt >= since)
            .GroupBy(d => d.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        // Moi luot hoi cua nguoi dung = 1 tin nhan role user.
        var chatsRaw = await db.ChatMessages
            .Where(m => m.CreatedAt >= since && m.Role == ChatRole.user)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var daily = Enumerable.Range(0, days)
            .Select(i => since.AddDays(i))
            .Select(d => new UsageDailyDto(
                d.ToString("MM-dd"),
                uploadsRaw.FirstOrDefault(r => r.Date == d)?.Count ?? 0,
                chatsRaw.FirstOrDefault(r => r.Date == d)?.Count ?? 0))
            .ToList();

        // Hoat dong gan day: gop upload + chat + giao dich hoan tat, lay 15 su kien moi nhat.
        var recentUploads = await db.Documents
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt).Take(15)
            .Select(d => new { d.CreatedAt, User = d.User.Username, Action = "Upload", Target = d.Title })
            .ToListAsync();

        var recentChats = await db.ChatMessages
            .Where(m => m.Role == ChatRole.user)
            .OrderByDescending(m => m.CreatedAt).Take(15)
            .Select(m => new { m.CreatedAt, User = m.Session.User.Username, Action = "Chat AI", Target = m.Content })
            .ToListAsync();

        var recentTxns = await db.Transactions
            .Where(t => t.Status == PaymentStatus.completed)
            .OrderByDescending(t => t.CreatedAt).Take(15)
            .Select(t => new { t.CreatedAt, User = t.User.Username, Action = "Thanh toán",
                Target = t.SubscriptionPackage != null ? t.SubscriptionPackage.Name : "Nạp dung lượng" })
            .ToListAsync();

        var recentActivity = recentUploads.Concat(recentChats).Concat(recentTxns)
            .OrderByDescending(x => x.CreatedAt).Take(15)
            .Select(x => new ActivityDto(
                x.CreatedAt.ToString("dd/MM HH:mm"),
                x.User, x.Action,
                x.Target.Length > 60 ? x.Target[..60] + "…" : x.Target))
            .ToList();

        // DAU/MAU va luot tim kiem chua duoc log -> null => FE hien "chua co du lieu".
        return new AdminUsageResponse(daily, recentActivity, daily.Sum(d => d.Uploads), daily.Sum(d => d.Chats), null, null, null);
    }

    private async Task<decimal> ComputeMrrAsync()
    {
        var now = DateTime.UtcNow;
        var active = await db.UserSubscriptions
            .Where(s => s.Status == "active" && s.EndDate >= now)
            .Select(s => new { s.Package.Price, s.Package.DurationDays })
            .ToListAsync();
        // Quy doanh thu ve theo thang (MRR): gia * 30 / so ngay cua goi.
        return Math.Round(active.Sum(s => s.DurationDays > 0 ? s.Price * 30m / s.DurationDays : s.Price), 0);
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
