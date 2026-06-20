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
var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Default"));
dataSourceBuilder.MapEnum<DocVisibility>("ai_study_hub.doc_visibility");
dataSourceBuilder.MapEnum<CloudStatus>("ai_study_hub.cloud_status");
dataSourceBuilder.MapEnum<ChatRole>("ai_study_hub.chat_role");
dataSourceBuilder.MapEnum<PaymentStatus>("ai_study_hub.payment_status");
dataSourceBuilder.MapEnum<PaymentMethod>("ai_study_hub.payment_method");
dataSourceBuilder.MapEnum<PurchaseType>("ai_study_hub.purchase_type");
dataSourceBuilder.MapEnum<UserRole>("ai_study_hub.user_role");
dataSourceBuilder.EnableUnmappedTypes();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(dataSource, x =>
{
    x.MapEnum<DocVisibility>("ai_study_hub.doc_visibility");
    x.MapEnum<CloudStatus>("ai_study_hub.cloud_status");
    x.MapEnum<ChatRole>("ai_study_hub.chat_role");
    x.MapEnum<PaymentStatus>("ai_study_hub.payment_status");
    x.MapEnum<PaymentMethod>("ai_study_hub.payment_method");
    x.MapEnum<PurchaseType>("ai_study_hub.purchase_type");
    x.MapEnum<UserRole>("ai_study_hub.user_role");
    x.MigrationsHistoryTable("__EFMigrationsHistory", "ai_study_hub");
}));

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

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CloudinaryService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddScoped<DocumentTextExtractor>();
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

builder.Services.AddControllers();
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

// Seed admin account
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Ensure "admin" role exists
    var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "admin");
    if (adminRole == null)
    {
        adminRole = new AIStudyHub.Api.Models.Role { Name = "admin", Description = "Quản trị viên toàn quyền hệ thống" };
        db.Roles.Add(adminRole);
        await db.SaveChangesAsync();
    }

    // Ensure "user" role exists
    var userRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "user");
    if (userRole == null)
    {
        userRole = new AIStudyHub.Api.Models.Role { Name = "user", Description = "Người dùng thông thường" };
        db.Roles.Add(userRole);
        await db.SaveChangesAsync();
    }

    // Seed admin user if not exists
    var adminEmail = "admin@aistudyhub.com";
    var adminExists = await db.Users.AnyAsync(u => u.Email == adminEmail);
    if (!adminExists)
    {
        var adminUser = new AIStudyHub.Api.Models.User
        {
            Username = "admin",
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
            RoleId = adminRole.Id
        };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();
        Console.WriteLine("✅ Admin account seeded: admin@aistudyhub.com / Admin@123456");
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
