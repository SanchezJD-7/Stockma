using Microsoft.AspNetCore.Mvc;
using Stockma.Application.Common;

namespace Stockma.Api.Middleware;
public sealed class TenantMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Tenant-ID";

    public static readonly IReadOnlySet<string> UnauthenticatedPaths =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/api/auth/login",
            "/api/auth/confirm-device",
        };

    public async Task InvokeAsync(HttpContext httpContext, ITenantContext tenantContext)
    {
        if (IsUnauthenticatedSurface(httpContext.Request.Path))
        {
            await next(httpContext);
            return;
        }

        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues)
            || string.IsNullOrWhiteSpace(headerValues.ToString()))
        {
            await WriteProblemDetailsAsync(
                httpContext,
                "TENANT_HEADER_MISSING",
                $"El header {HeaderName} es obligatorio.");
            return;
        }

        if (!Guid.TryParse(headerValues.ToString(), out var tenantId) || tenantId == Guid.Empty)
        {
            await WriteProblemDetailsAsync(
                httpContext,
                "TENANT_HEADER_INVALID",
                $"El header {HeaderName} debe ser un GUID válido y distinto de Guid.Empty.");
            return;
        }

        tenantContext.Set(tenantId);

        await next(httpContext);
    }

    private static bool IsUnauthenticatedSurface(PathString path) =>
        UnauthenticatedPaths.Contains((path.Value ?? string.Empty).TrimEnd('/'));

    private static async Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        string errorCode,
        string detail)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Tenant no resuelto",
            Detail = detail,
            Extensions = { ["errorCode"] = errorCode },
        };

        await httpContext.Response.WriteAsJsonAsync(problem);
    }
}
