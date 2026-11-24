using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stockma.Application.Products.Exceptions;
using Stockma.Application.Waitlist.Exceptions;
using Stockma.Domain.Exceptions;

namespace Stockma.Api.Middleware;

public sealed class DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware>? logger = null)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (DomainException exception)
        {
            var status = MapStatus(exception);

            httpContext.Response.StatusCode = status;

            await httpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = status,
                    Title = TitleFor(status),
                    Detail = exception.Message,
                    Extensions = { ["errorCode"] = exception.ErrorCode },
                },
                options: null,
                contentType: ProblemResponses.ContentType);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Excepción no dominada al procesar {Path}", httpContext.Request.Path);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await httpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Error inesperado",
                    Detail = "Ocurrió un error procesando la solicitud.",
                    Extensions = { ["errorCode"] = "INTERNAL_ERROR" },
                },
                options: null,
                contentType: ProblemResponses.ContentType);
        }
    }

    private static int MapStatus(DomainException exception) => exception switch
    {
        InvalidCredentialsException or AccountDisabledException or OtpNotUsableException or RefreshTokenNotUsableException => StatusCodes.Status401Unauthorized,
        PhoneNotEnrolledException => StatusCodes.Status403Forbidden,
        EmailAlreadyRegisteredException or PhoneChangePendingException => StatusCodes.Status409Conflict,
        NegativeStockException => StatusCodes.Status422UnprocessableEntity,
        ConcurrencyConflictException => StatusCodes.Status409Conflict,
        SkuDuplicateException or BarcodeDuplicateException => StatusCodes.Status409Conflict,
        WaitlistEmailAlreadyRegisteredException => StatusCodes.Status409Conflict,
        SkuImmutableException or ValidationFailedException => StatusCodes.Status400BadRequest,
        ProductNotFoundException
            or BarcodeNotFoundException
            or ProductNotFoundForBatchException
            or BatchNotFoundException
            or TrustedDeviceNotFoundException
            or UserNotFoundException => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "No autenticado",
        StatusCodes.Status403Forbidden => "Prohibido",
        StatusCodes.Status409Conflict => "Conflicto",
        StatusCodes.Status404NotFound => "Recurso no encontrado",
        StatusCodes.Status422UnprocessableEntity => "Operación improcesable",
        _ => "Solicitud inválida",
    };
}
