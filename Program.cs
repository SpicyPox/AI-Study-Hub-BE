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
#pragma warning disable CS0618 
NpgsqlConnection.GlobalTypeMapper.MapEnum<UserRole>("ai_study_hub.user_role");
NpgsqlConnection.GlobalTypeMapper.MapEnum<DocVisibility>("ai_study_hub.doc_visibility");
NpgsqlConnection.GlobalTypeMapper.MapEnum<CloudStatus>("ai_study_hub.cloud_status");
NpgsqlConnection.GlobalTypeMapper.MapEnum<ChatRole>("ai_study_hub.chat_role");
NpgsqlConnection.GlobalTypeMapper.MapEnum<PaymentStatus>("ai_study_hub.payment_status");
NpgsqlConnection.GlobalTypeMapper.MapEnum<PaymentMethod>("ai_study_hub.payment_method");
#pragma warning restore CS0618 

var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Default"));
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(dataSource));

// Auth
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
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CloudinaryService>();
builder.Services.AddScoped<ClaudeService>();
builder.Services.AddHttpClient("Anthropic");

// CORS — allow frontend dev server
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
