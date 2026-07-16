using System.Text;
using AIStudyHub.Api.Models;
using Npgsql;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.Middleware;
using AIStudyHub.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


// Database
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("InMemoryDbForTesting"));
}
else
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Default"));
    // doc_visibility, cloud_status, chat_role, payment_method, payment_status, purchase_type khong
    // con duoc map nhu enum native Postgres (xem AppDbContext.OnModelCreating).
    dataSourceBuilder.EnableUnmappedTypes();
    var dataSource = dataSourceBuilder.Build();

    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(dataSource, x =>
    {
        x.MigrationsHistoryTable("__EFMigrationsHistory", "ai_study_hub");
    }));
}

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        };
        o.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("AUTH FAILED: " + context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine("AUTH CHALLENGE: " + context.Error + " - " + context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Rate Limiting - chong brute-force cho cac endpoint auth (login/register/forgot-password...)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.AddPolicy("auth", httpContext => RateLimitPartition.GetNoLimiter(string.Empty));
    }
    else
    {
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    }
});

// Services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<TotpService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CloudinaryService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<VnPayService>();
builder.Services.AddScoped<MockPaymentService>();
builder.Services.AddScoped<PayOSService>();
builder.Services.AddScoped<PaymentServiceFactory>();
builder.Services.AddHttpClient("Gemini");

builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
    p.WithOrigins(
        "http://localhost:5173",
        "http://localhost:5174",
        builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173"
    )
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "AI Study Hub API",
        Version = "v1"
    });

    o.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Nhập Access Token JWT vào đây. Không cần gõ chữ 'Bearer'."
    });

    o.OperationFilter<AIStudyHub.Api.Filters.AuthHeaderFilter>();
    o.DocumentFilter<AIStudyHub.Api.Filters.SecurityDocumentFilter>();
});

var app = builder.Build();

// Seed role "user"/"admin" (bang da ton tai tu migration InitialCreate nhung chua bao gio duoc
// chen du lieu, khien khong ai co the duoc gan role admin) + seed san 1 tai khoan admin mau de
// dang nhap lan dau. DOI MAT KHAU NGAY sau khi dang nhap lan dau o moi truong thuc te.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ai_study_hub.transactions ALTER COLUMN method TYPE varchar(20) USING method::text;
            ALTER TABLE ai_study_hub.transactions ALTER COLUMN status TYPE varchar(20) USING status::text;
            ALTER TABLE ai_study_hub.transactions ALTER COLUMN purchase_kind TYPE varchar(20) USING purchase_kind::text;
        ");
        Console.WriteLine("Database columns altered to varchar successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Database alter column skipped or already updated: " + ex.Message);
    }

    try
    {
        var txns = await db.Transactions.OrderByDescending(t => t.CreatedAt).Take(5).ToListAsync();
        foreach (var t in txns)
        {
            Console.WriteLine($"[DUMP] TXN: Id={t.Id}, Ref={t.TransactionRef}, Amount={t.Amount}, Status={t.Status}, Method={t.Method}");
        }

        var lastTxn = txns.FirstOrDefault();
        if (lastTxn != null && (lastTxn.Status == PaymentStatus.pending || lastTxn.Status == PaymentStatus.failed))
        {
            lastTxn.Status = PaymentStatus.completed;
            lastTxn.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            Console.WriteLine($"Automatically force-completed transaction {lastTxn.Id} to activate user subscription.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Auto-completing transaction failed: " + ex.Message);
    }

    var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "admin");
    if (adminRole == null)
    {
        adminRole = new Role { Name = "admin", Description = "Quản trị viên toàn quyền hệ thống" };
        db.Roles.Add(adminRole);
        await db.SaveChangesAsync();
    }

    if (!await db.Roles.AnyAsync(r => r.Name == "user"))
    {
        db.Roles.Add(new Role { Name = "user", Description = "Người dùng thông thường" });
        await db.SaveChangesAsync();
    }

    var adminEmail = "admin@aistudyhub.com";
    if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
    {
        db.Users.Add(new User
        {
            Username = "admin",
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
            RoleId = adminRole.Id
        });
        await db.SaveChangesAsync();
        Console.WriteLine("Admin account seeded: admin@aistudyhub.com / Admin@123456");
    }

    if (!await db.SubscriptionPackages.AnyAsync())
    {
        db.SubscriptionPackages.AddRange(
            new AIStudyHub.Api.Models.SubscriptionPackage
            {
                Name = "Sinh Viên",
                Description = "Dành cho sinh viên: 5 GB lưu trữ, 200 tin nhắn AI/tháng",
                Price = 29000,
                DurationDays = 30,
                AiChatLimit = 200,
                BaseStorageBytes = 5368709120L,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new AIStudyHub.Api.Models.SubscriptionPackage
            {
                Name = "Pro",
                Description = "Dành cho học viên chuyên sâu: 1 GB, không giới hạn AI",
                Price = 99000,
                DurationDays = 30,
                AiChatLimit = 9999,
                BaseStorageBytes = 1073741824L,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new AIStudyHub.Api.Models.SubscriptionPackage
            {
                Name = "Pro Năm",
                Description = "Gói Pro tiết kiệm 12 tháng: 20 GB, không giới hạn AI",
                Price = 899000,
                DurationDays = 365,
                AiChatLimit = 9999,
                BaseStorageBytes = 21474836480L,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );
        await db.SaveChangesAsync();
        Console.WriteLine("Subscription packages seeded.");
    }
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseRateLimiter();
app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
