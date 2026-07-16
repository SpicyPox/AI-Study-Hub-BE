namespace AIStudyHub.Api.DTOs.Admin;

public record UpdateUserRequest(string? Status, string? Role);

public record AdminUserDto(
    Guid Id, string Name, string Email, string Role,
    string Status, int DocCount, long StorageBytes, int TokensUsed, DateTime CreatedAt
);

public record AdminStatsDto(int TotalUsers, int TotalDocs, long TotalTokenUsage, decimal Mrr);
public record TokenDailyDto(string Date, int Tokens);
public record TokenStatsResponse(IEnumerable<TokenDailyDto> Daily, int TotalToday, int TotalAllTime);
public record AdminUserListResponse(IEnumerable<AdminUserDto> Users, int Total);
public record UpdateUserRoleRequest(string Role);
public record HideDocumentRequest(bool Hide);

// ── Subscriptions (admin) ──────────────────────────────────────────────────
public record AdminSubscriptionDto(
    Guid Id, string UserName, string UserEmail, string Plan,
    decimal Amount, string Status, DateTime StartDate, DateTime EndDate
);
public record AdminSubscriptionsResponse(
    IEnumerable<AdminSubscriptionDto> Items,
    decimal Mrr, int Active, int PastDue,
    Dictionary<string, int> PlanBreakdown
);

// ── Usage (admin) ──────────────────────────────────────────────────────────
public record UsageDailyDto(string Date, int Uploads, int Chats);
public record ActivityDto(string Time, string User, string Action, string Target);
public record AdminUsageResponse(
    IEnumerable<UsageDailyDto> Daily,
    IEnumerable<ActivityDto> RecentActivity,
    int UploadsTotal, int ChatsTotal,
    // Cac chi so DB chua log -> null => FE hien "chua co du lieu".
    int? Dau, int? Mau, int? SearchTotal
);
