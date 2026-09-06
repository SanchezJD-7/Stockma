using Microsoft.AspNetCore.Mvc;
using Stockma.Application.Products.Exceptions;
using Stockma.Domain.Exceptions;

namespace Stockma.Api.Middleware;

public sealed class DomainExceptionMiddleware(RequestDelegate next)
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

            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = TitleFor(status),
                Detail = exception.Message,
                Extensions = { ["errorCode"] = exception.ErrorCode },
            });
        }
    }

    private static int MapStatus(DomainException exception) => exception switch
    {
        NegativeStockException => StatusCodes.Status422UnprocessableEntity,
        ConcurrencyConflictException => StatusCodes.Status409Conflict,
        SkuDuplicateException or BarcodeDuplicateException => StatusCodes.Status409Conflict,
        SkuImmutableException => StatusCodes.Status400BadRequest,
        ProductNotFoundException
            or BarcodeNotFoundException
            or ProductNotFoundForBatchException
            or BatchNotFoundException => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status409Conflict => "Conflicto",
        StatusCodes.Status404NotFound => "Recurso no encontrado",
        StatusCodes.Status422UnprocessableEntity => "Operación improcesable",
        _ => "Solicitud inválida",
    };
}
