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
