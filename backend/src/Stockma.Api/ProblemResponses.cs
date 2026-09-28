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
        var problem = new ValidationProblemDetails(modelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Solicitud inválida",
            Extensions = { ["errorCode"] = ValidationFailedCode },
        };

        return new BadRequestObjectResult(problem) { ContentTypes = { ContentType } };
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
