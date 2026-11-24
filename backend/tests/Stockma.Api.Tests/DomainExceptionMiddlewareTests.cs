using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Stockma.Api.Middleware;
using Stockma.Application.Products.Exceptions;
using Stockma.Application.Waitlist.Exceptions;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;

namespace Stockma.Api.Tests;
public class DomainExceptionMiddlewareTests
{
    private static async Task<(int Status, string? ErrorCode)> InvokeAsync(DomainException exception)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var middleware = new DomainExceptionMiddleware(_ => throw exception);

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var payload = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);

        var errorCode = payload.TryGetProperty("errorCode", out var value) ? value.GetString() : null;

        return (httpContext.Response.StatusCode, errorCode);
    }

    [Fact]
    public async Task ValidationFailed_MapsTo400()
    {
        var (status, errorCode) = await InvokeAsync(new ValidationFailedException("El deviceId es obligatorio."));

        status.Should().Be(StatusCodes.Status400BadRequest);
        errorCode.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task DomainErrors_AreWrittenAsProblemJson()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await new DomainExceptionMiddleware(_ => throw new ValidationFailedException("x")).InvokeAsync(httpContext);

        httpContext.Response.ContentType.Should().StartWith("application/problem+json", "auth-api.md: formato de error");
    }

    [Fact]
    public async Task NegativeStock_MapsTo422()
    {
        var (status, errorCode) = await InvokeAsync(new NegativeStockException(3, -5));

        status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        errorCode.Should().Be("BATCH_NEGATIVE_STOCK");
    }

    [Fact]
    public async Task ConcurrencyConflict_MapsTo409()
    {
        var (status, errorCode) = await InvokeAsync(
            new ConcurrencyConflictException(nameof(Batch), Guid.NewGuid()));

        status.Should().Be(StatusCodes.Status409Conflict);
        errorCode.Should().Be(
            "CONCURRENCY_CONFLICT",
            "es el camino del 409 de FR-014, que no se puede disparar de forma determinista end-to-end");
    }

    [Fact]
    public async Task SkuDuplicate_MapsTo409()
    {
        var (status, errorCode) = await InvokeAsync(new SkuDuplicateException("SKU-1"));

        status.Should().Be(StatusCodes.Status409Conflict);
        errorCode.Should().Be("PRODUCT_SKU_DUPLICATE");
    }

    [Fact]
    public async Task SkuImmutable_MapsTo400()
    {
        var (status, errorCode) = await InvokeAsync(new SkuImmutableException("SKU-1", "SKU-2"));

        status.Should().Be(StatusCodes.Status400BadRequest);
        errorCode.Should().Be("PRODUCT_SKU_IMMUTABLE");
    }

    [Fact]
    public async Task BatchNotFound_MapsTo404()
    {
        var (status, errorCode) = await InvokeAsync(new BatchNotFoundException(Guid.NewGuid()));

        status.Should().Be(StatusCodes.Status404NotFound);
        errorCode.Should().Be("BATCH_NOT_FOUND");
    }

    [Fact]
    public async Task ProductNotFoundForBatch_MapsTo404()
    {
        var (status, errorCode) = await InvokeAsync(new ProductNotFoundForBatchException(Guid.NewGuid()));

        status.Should().Be(StatusCodes.Status404NotFound);
        errorCode.Should().Be("BATCH_PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task WaitlistEmailDuplicate_MapsTo409()
    {
        var (status, errorCode) = await InvokeAsync(
            new WaitlistEmailAlreadyRegisteredException("juan@ejemplo.com"));

        status.Should().Be(StatusCodes.Status409Conflict, "spec 003: el correo duplicado es 409");
        errorCode.Should().Be("WAITLIST_EMAIL_DUPLICATE");
    }

    [Fact]
    public async Task NonDomainException_MapsTo500ProblemJsonWithoutStackTrace()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var middleware = new DomainExceptionMiddleware(
            _ => throw new InvalidOperationException("falla de infraestructura"));

        var act = async () => await middleware.InvokeAsync(httpContext);

        await act.Should().NotThrowAsync("el catch-all convierte la excepción en 500 problem+json");

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var payload = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);

        payload.GetProperty("errorCode").GetString().Should().Be("INTERNAL_ERROR");
        payload.TryGetProperty("stackTrace", out _).Should().BeFalse("el 500 jamás filtra stack trace");
        payload.GetProperty("detail").GetString().Should()
            .NotContain("InvalidOperationException", "el detalle no expone el tipo de excepción");
    }
}
