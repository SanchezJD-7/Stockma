using Microsoft.AspNetCore.Mvc;
using Stockma.Application.Common;
using Stockma.Application.Identity;

namespace Stockma.Api.Middleware;
public sealed class TenantMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Tenant-ID";

    public static readonly IReadOnlySet<string> UnauthenticatedPaths =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/api/auth/login",
            "/api/auth/confirm-device",
            "/api/auth/refresh",
            "/api/auth/logout",
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
                StatusCodes.Status400BadRequest,
                "Tenant no resuelto",
                "TENANT_HEADER_MISSING",
                $"El header {HeaderName} es obligatorio.");
            return;
        }

        if (!Guid.TryParse(headerValues.ToString(), out var tenantId) || tenantId == Guid.Empty)
        {
            await WriteProblemDetailsAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                "Tenant no resuelto",
                "TENANT_HEADER_INVALID",
                $"El header {HeaderName} debe ser un GUID válido y distinto de Guid.Empty.");
            return;
        }

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var tokenTenantIdClaim = httpContext.User.FindFirst(StockmaClaimTypes.TenantId)?.Value;

            if (!Guid.TryParse(tokenTenantIdClaim, out var tokenTenantId) || tokenTenantId != tenantId)
            {
                await WriteProblemDetailsAsync(
                    httpContext,
                    StatusCodes.Status403Forbidden,
                    "Tenant no coincide",
                    "TENANT_MISMATCH",
                    $"El {HeaderName} no coincide con el tenant del token.");
                return;
            }
        }

        tenantContext.Set(tenantId);

        await next(httpContext);
    }

    private static bool IsUnauthenticatedSurface(PathString path) =>
        UnauthenticatedPaths.Contains((path.Value ?? string.Empty).TrimEnd('/'));

    private static async Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        int status,
        string title,
        string errorCode,
        string detail)
    {
        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Extensions = { ["errorCode"] = errorCode },
        };

        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: ProblemResponses.ContentType);
    }
}
