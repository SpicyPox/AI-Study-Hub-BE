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

var builder = WebApplication.CreateBuilder(args);


// Database
var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Default"));
// doc_visibility, cloud_status, chat_role khong con duoc map nhu enum native Postgres
// (xem AppDbContext.OnModelCreating).
dataSourceBuilder.MapEnum<PaymentStatus>("ai_study_hub.payment_status");
dataSourceBuilder.MapEnum<PaymentMethod>("ai_study_hub.payment_method");
dataSourceBuilder.MapEnum<PurchaseType>("ai_study_hub.purchase_type");
dataSourceBuilder.EnableUnmappedTypes();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(dataSource, x =>
{
    x.MapEnum<PaymentStatus>("ai_study_hub.payment_status");
    x.MapEnum<PaymentMethod>("ai_study_hub.payment_method");
    x.MapEnum<PurchaseType>("ai_study_hub.purchase_type");
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

// Seed bang roles ("user", "admin") neu chua co - bang da ton tai tu migration InitialCreate
// nhung chua bao gio duoc chen du lieu, khien khong ai co the duoc gan role admin.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    foreach (var name in new[] { "user", "admin" })
    {
        if (!await db.Roles.AnyAsync(r => r.Name == name))
            db.Roles.Add(new Role { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow });
    }
    await db.SaveChangesAsync();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
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
