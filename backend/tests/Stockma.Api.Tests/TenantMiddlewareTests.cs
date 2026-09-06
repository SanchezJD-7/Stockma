using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Stockma.Api.Middleware;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Api.Tests;
public class TenantMiddlewareTests
{
    private const string HeaderName = "X-Tenant-ID";

    [Fact]
    public async Task ValidTenant_ExposesTenantIdAndContinuesPipeline()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = tenantId.ToString();

        var tenantContext = new TenantContext();
        Guid? tenantSeenByPipeline = null;

        var middleware = new TenantMiddleware(_ =>
        {
            tenantSeenByPipeline = tenantContext.TenantId;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, tenantContext);

        tenantSeenByPipeline.Should().Be(tenantId, "el TenantContext debe estar poblado durante toda la request");
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task MissingHeader_Returns400AndSkipsPipeline()
    {
        var httpContext = new DefaultHttpContext();
        var pipelineExecuted = false;

        var middleware = new TenantMiddleware(_ =>
        {
            pipelineExecuted = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, new TenantContext());

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        pipelineExecuted.Should().BeFalse("sin tenant resuelto el pipeline no debe ejecutarse");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidHeader_Returns400AndSkipsPipeline(string headerValue)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = headerValue;
        var pipelineExecuted = false;

        var middleware = new TenantMiddleware(_ =>
        {
            pipelineExecuted = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, new TenantContext());

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        pipelineExecuted.Should().BeFalse();
    }
}
