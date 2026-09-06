using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Stockma.Api.Middleware;
using Stockma.Application.Products.Exceptions;
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
    public async Task NonDomainException_IsNotSwallowed()
    {
        var httpContext = new DefaultHttpContext();
        var middleware = new DomainExceptionMiddleware(
            _ => throw new InvalidOperationException("falla de infraestructura"));

        var act = async () => await middleware.InvokeAsync(httpContext);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "un error que no es de negocio no debe convertirse en 400: debe propagarse y quedar visible");
    }
}
