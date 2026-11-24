using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Stockma.Api;

public static class ProblemResponses
{
    public const string ContentType = "application/problem+json";
    public const string ValidationFailedCode = "VALIDATION_FAILED";
    public const string RateLimitedCode = "AUTH_RATE_LIMITED";

    public static IActionResult ValidationFailed(ModelStateDictionary modelState)
    {
        var errors = modelState.ToDictionary(
            entry => NormalizeKey(entry.Key),
            entry => entry.Value!.Errors
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? (error.Exception?.Message ?? "Valor inválido.")
                    : error.ErrorMessage)
                .ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Solicitud inválida",
            Extensions = { ["errorCode"] = ValidationFailedCode },
        };

        return new BadRequestObjectResult(problem) { ContentTypes = { ContentType } };
    }

    private static string NormalizeKey(string key)
    {
        var name = key[(key.LastIndexOf('.') + 1)..];

        return name.Length == 0
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static async ValueTask WriteRateLimitedAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Demasiados intentos",
                Detail = "Superaste el límite de intentos de autenticación. Espera un minuto y vuelve a intentarlo.",
                Extensions = { ["errorCode"] = RateLimitedCode },
            },
            options: null,
            contentType: ContentType,
            cancellationToken: cancellationToken);
    }
}
