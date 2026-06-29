using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AIStudyHub.Api.Filters;

/// <summary>
/// Injects the Bearer security requirement into every operation
/// marked with x-requires-auth by AuthHeaderFilter.
/// This ensures the Swagger UI lock/Authorize button sends the JWT token.
/// </summary>
public class SecurityDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Make sure the Bearer scheme is registered in components
        swaggerDoc.Components ??= new OpenApiComponents();
        if (swaggerDoc.Components.SecuritySchemes == null ||
            !swaggerDoc.Components.SecuritySchemes.ContainsKey("Bearer"))
            return;   // AddSecurityDefinition() must be called first in Program.cs

        // Build a reference to the registered "Bearer" scheme
        var bearerRef = new OpenApiSecuritySchemeReference("Bearer", swaggerDoc, null);

        // Apply to every operation that requires auth
        if (swaggerDoc.Paths == null) return;
        foreach (var path in swaggerDoc.Paths.Values)
        {
            if (path.Operations == null) continue;
            foreach (var operation in path.Operations.Values)
            {
                if (operation.Extensions == null ||
                    !operation.Extensions.ContainsKey("x-requires-auth"))
                    continue;

                operation.Security ??= new List<OpenApiSecurityRequirement>();

                var req = new OpenApiSecurityRequirement
                {
                    { bearerRef, new List<string>() }
                };
                operation.Security.Add(req);

                // Remove the marker extension
                operation.Extensions.Remove("x-requires-auth");
            }
        }
    }
}
