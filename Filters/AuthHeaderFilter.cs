using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AIStudyHub.Api.Filters;

/// <summary>
/// Marks each [Authorize] operation so SecurityDocumentFilter can inject the security requirement.
/// </summary>
public class AuthHeaderFilter : IOperationFilter
{
    // We use a custom object as a marker extension value
    private sealed class BoolExtension(bool value) : IOpenApiExtension
    {
        public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
            => writer.WriteValue(value);
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize =
            context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true ||
            context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();

        var allowAnonymous = context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();

        if (!hasAuthorize || allowAnonymous) return;

        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions["x-requires-auth"] = new BoolExtension(true);
    }
}
