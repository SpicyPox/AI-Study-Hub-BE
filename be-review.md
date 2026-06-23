# Backend Code Review — AI Study Hub BE

**Stack:** ASP.NET Core (.NET 10) · EF Core 10.0.8 · Npgsql 10.0.2 · PostgreSQL (`ai_study_hub` schema)  
**Date:** 2026-06-22  
**Scope:** Controllers, Services, DTOs, Middleware, Migrations

---

## Tóm tắt nhanh

| Mức độ | Số lượng |
|--------|---------|
| 🔴 CRITICAL (không compile / mất dữ liệu) | 1 → **ĐÃ SỬA** |
| 🟠 HIGH (security / correctness bug) | 5 → 2 đã sửa, 3 còn lại |
| 🟡 MEDIUM (logic / quality) | 6 |
| 🟢 LOW (style / minor) | 4 |

---

## 🔴 CRITICAL

### C1 — Migration file bị merge conflict (5 block) ✅ ĐÃ SỬA

**File:** `Migrations/20260616064723_InitialCreate.cs`

Git merge với branch cũ (`ss2`) tạo 5 unresolved conflict block, class bị đổi tên thành `ss2`. Project không compile.

**Đã sửa trong session này:**
- Class name: `ss2` → `InitialCreate`
- Conflict 1: giữ `role_id` (UUID FK to roles) thay vì `role` enum column
- Conflict 2: giữ FK definition + xóa table comment dài
- Conflict 3: giữ `DocVisibility` enum type, thêm `defaultValue: false` cho `is_deleted`
- Conflict 4: giữ `PaymentStatus`/`PaymentMethod` enum types (typed thay vì string)
- Conflict 5: merge cả hai index set — giữ `IX_users_role_id` và thêm `idx_users_email_lower`
- **Bonus fix:** thêm `Annotation("Npgsql:Enum:ai_study_hub.user_role", "admin,user")` vào `AlterDatabase()` — thiếu trong bản gốc, khiến EF migration không tạo enum type này

---

## 🟠 HIGH

### H1 — R19 Bug: Download public doc bị chặn ✅ ĐÃ SỬA

**File:** `Controllers/DocumentsController.cs` — `Download()`

```csharp
// TRƯỚC (bug): chỉ owner mới download được
.FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId())

// SAU (fix): owner HOẶC doc public đều download được
.FirstOrDefaultAsync(d => d.Id == id && (d.UserId == uid || d.Visibility == DocVisibility.@public))
```

Đồng thời bỏ `[Authorize]` khỏi action này — không cần đăng nhập để xem public doc.  
`GetById()` và `Summarize()` đã xử lý đúng; chỉ `Download()` bị sót.

---

### H2 — Refresh Token không rotate ⚠️ Chưa apply

**File:** `Services/AuthService.cs` · `Controllers/AuthController.cs`

`RefreshAsync` trả về `string` (chỉ access token). Controller trả `new { accessToken = token }` — client không nhận refreshToken mới. Token cũ vô hạn hiệu lực trong DB.

**Fix file đã có:** `be-fixes/AuthService.cs`, `be-fixes/AuthController.cs`  
→ Cần copy vào source chính hoặc dùng lệnh: `cp be-fixes/AuthService.cs Services/AuthService.cs`

---

### H3 — User enumeration qua ForgotPassword ⚠️ Chưa apply

**File:** `Services/AuthService.cs` — `ForgotPasswordAsync()`

```csharp
// Hiện tại: ném 404 khi email không tồn tại → attacker biết email nào đã đăng ký
throw new KeyNotFoundException("Email không tồn tại.");

// Fix: luôn trả 200
if (user == null) return; // không báo lỗi, không gửi email
```

**Fix file đã có:** `be-fixes/AuthService.cs`

---

### H4 — ErrorHandlingMiddleware lộ exception message ra client

**File:** `Middleware/ErrorHandlingMiddleware.cs`

```csharp
var body = JsonSerializer.Serialize(new { message = ex.Message });
```

`ex.Message` từ DB errors, connection failures, hay `InvalidOperationException` có thể chứa thông tin nhạy cảm (tên bảng, connection string fragment, stack context). Production cần che.

**Fix đề xuất:**
```csharp
var isDevEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
var message = isDevEnv ? ex.Message : ex switch {
    UnauthorizedAccessException => "Unauthorized",
    KeyNotFoundException => "Not found",
    InvalidOperationException => "Bad request",
    _ => "Internal server error"
};
var body = JsonSerializer.Serialize(new { message });
```

---

### H5 — Không có Rate Limiting trên auth endpoints ⚠️ Chưa apply

**File:** `Controllers/AuthController.cs`

Login, ForgotPassword, RefreshToken chưa có `[EnableRateLimiting("auth")]`. Brute-force attack không bị chặn.

**Fix file đã có:** `be-fixes/Program.cs`, `be-fixes/AuthController.cs`

---

## 🟡 MEDIUM

### M1 — `tokensUsed` hardcode 0 trong AdminController

**File:** `Controllers/AdminController.cs`

```csharp
TokensUsed = 0  // hardcoded ở mọi nơi
```

Bảng `chat_messages` không có cột `tokens_used`. Nếu cần tính token dùng thực, cần thêm cột vào model + migration.

**Tác động:** Dashboard admin hiển thị 0 AI tokens cho tất cả users, không phản ánh thực tế.

---

### M2 — Dead code trong SubjectsController

**File:** `Controllers/SubjectsController.cs` — `GetAll()`

```csharp
var uid = UserId(); // khai báo nhưng không dùng ở đâu
```

Xóa dòng này để tránh compiler warning.

---

### M3 — Document search dùng C# `.ToLower().Contains()` thay vì DB-level

**File:** `Controllers/DocumentsController.cs` — `Search()`

EF Core dịch `d.Title.ToLower().Contains(q)` thành `LOWER(title) LIKE '%q%'` — không dùng index, full table scan trên data lớn.

**Fix đề xuất:**
```csharp
.Where(d => EF.Functions.ILike(d.Title, $"%{q}%"))
```
`ILike` của Npgsql là case-insensitive và có thể tận dụng `pg_trgm` index nếu cần.

---

### M4 — `MinLength(6)` trên password quá ngắn

**File:** `DTOs/Auth/AuthDtos.cs`

```csharp
[Required, MinLength(6)] string Password  // RegisterRequest
[MinLength(6)] string? NewPassword        // UpdateMeRequest + ResetPasswordRequest
```

Industry standard tối thiểu là 8. Fix files trong `be-fixes/` đã dùng `MinLength(8)`.

---

### M5 — DocumentTextExtractor cache không invalidate khi doc bị xóa/update

**File:** `Services/DocumentTextExtractor.cs`

```csharp
cache.Set(cacheKey, text, TimeSpan.FromMinutes(30));
```

Nếu document bị xóa hoặc file trên Cloudinary bị replace, cache vẫn trả nội dung cũ trong 30 phút. Cần gọi `cache.Remove($"doc_text_{doc.Id}")` trong `Delete()` và `Update()` của DocumentsController.

---

### M6 — GeminiService `maxOutputTokens` hardcode, không configurable

**File:** `Services/GeminiService.cs`

```csharp
generationConfig = new { maxOutputTokens = 1024 }   // StreamGeminiAsync
generationConfig = new { maxOutputTokens = 1536 }   // GenerateSummaryAsync
```

Nên đọc từ config: `config.GetValue<int>("Gemini:MaxOutputTokens", 1024)` để dễ điều chỉnh không cần rebuild.

---

## 🟢 LOW

### L1 — `purchase_type` mapping không nhất quán

`AppDbContext` không có `HasPostgresEnum<PurchaseType>()` (chỉ `MapEnum` ở `Program.cs`), nhưng migration `AlterDatabase()` có annotation cho `purchase_type`. Functionally OK nhưng gây confusion — nên thống nhất một cách.

### L2 — CloudinaryService ném exception nếu config thiếu ngay khi startup

**File:** `Services/CloudinaryService.cs` — constructor

```csharp
throw new InvalidOperationException("Cloudinary configuration is missing...");
```

Nếu `appsettings.json` thiếu Cloudinary keys, app crash ngay khi khởi động. Có thể chuyển sang lazy validation (chỉ throw khi thực sự upload) để app vẫn chạy được ở môi trường không cần cloud.

### L3 — EmailService tạo SmtpClient per-call và đọc config per-call

**File:** `Services/EmailService.cs`

Config được đọc và SmtpClient được tạo mới mỗi lần gọi `SendEmailAsync`. Hiệu suất thấp nếu gửi nhiều email. Nên resolve config một lần trong constructor.

### L4 — `appsettings.json` JWT key là placeholder

```json
"Key": "CHANGE_THIS_TO_A_RANDOM_32_CHAR_SECRET_KEY_12345"
```

Không nên commit placeholder dễ đoán này. Nên dùng `dotnet user-secrets` cho dev và environment variable cho production.

---

## Điểm tốt — Giữ nguyên

- **GeminiService**: Có stub mode khi thiếu API key, `SanitizeContents()` xử lý đúng alternating roles của Gemini API, streaming SSE chuẩn
- **CloudinaryService**: Build `PublicId` có namespace theo userId + UUID, tránh collision, dùng `RawUploadParams` đúng cho non-image files
- **DocumentTextExtractor**: Cache 30 phút hợp lý, fallback graceful cho unsupported formats
- **ErrorHandlingMiddleware**: Map exception types sang HTTP codes đúng (401/404/400/500)
- **AppDbContext**: Schema isolation `ai_study_hub` rõ ràng, 6 `HasPostgresEnum` đúng chỗ
- **AuthController + AuthService**: BCrypt hash, refresh token stored in DB, OTP flow cho forgot password
- **DocumentsController**: `GetById()` và `Summarize()` đã xử lý đúng public/private visibility

---

## Các fix đã thực hiện trong session này

| File | Thay đổi |
|------|---------|
| `Migrations/20260616064723_InitialCreate.cs` | Resolve 5 conflict blocks, đổi class `ss2` → `InitialCreate`, thêm `user_role` enum annotation |
| `Controllers/DocumentsController.cs` | Fix R19: Download cho phép public docs, bỏ `[Authorize]` |

## Fix đang chờ apply (files trong `be-fixes/`)

```bash
# Apply từ thư mục be-fixes/
cp be-fixes/AuthService.cs Services/AuthService.cs
cp be-fixes/AuthController.cs Controllers/AuthController.cs
cp be-fixes/Program.cs Program.cs
```
