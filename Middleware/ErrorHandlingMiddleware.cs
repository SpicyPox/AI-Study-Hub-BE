using System.Text.Json;

namespace AIStudyHub.Api.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = ex switch
            {
                UnauthorizedAccessException => 401,
                KeyNotFoundException => 404,
                InvalidOperationException => 400,
                _ => 500,
            };
            var body = JsonSerializer.Serialize(new { message = ex.Message });
            await ctx.Response.WriteAsync(body);
        }
    }
}
