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

    private static async Task<(bool Continued, int Status)> InvokeWithoutHeaderAsync(string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        var continued = false;
        var middleware = new TenantMiddleware(_ =>
        {
            continued = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, new TenantContext());

        return (continued, httpContext.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/confirm-device")]
    public async Task UnauthenticatedSurface_IsExemptFromTheHeader(string path)
    {
        var (continued, status) = await InvokeWithoutHeaderAsync(path);

        continued.Should().BeTrue("ADR-002: estas rutas ocurren ANTES de que exista un token");
        status.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("/API/AUTH/LOGIN")]
    [InlineData("/Api/Auth/Confirm-Device")]
    public async Task TheExemption_IsCaseInsensitive(string path)
    {
        var (continued, _) = await InvokeWithoutHeaderAsync(path);

        continued.Should().BeTrue("el routing de ASP.NET no distingue mayusculas; el middleware tampoco debe");
    }

    [Theory]
    [InlineData("/api/auth/login/")]
    [InlineData("/api/auth/confirm-device/")]
    public async Task TheExemption_ToleratesATrailingSlash(string path)
    {
        var (continued, _) = await InvokeWithoutHeaderAsync(path);

        continued.Should()
            .BeTrue("el routing matchea igual con barra final: si el middleware no, el cliente recibe un 400 incomprensible");
    }

    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/phone-number")]
    [InlineData("/api/products")]
    [InlineData("/api/batches")]
    [InlineData("/api/admin/tenant/branding")]
    [InlineData("/api/tenant/branding")]
    [InlineData("/api/auth")]
    [InlineData("/api/auth/login/extra")]
    public async Task EveryOtherRoute_StillRequiresTheHeader(string path)
    {
        var (continued, status) = await InvokeWithoutHeaderAsync(path);

        continued.Should().BeFalse($"'{path}' NO esta en la lista de exentas");
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Register_IsNotExempt_BecauseItCreatesInsideATenant()
    {
        var (continued, status) = await InvokeWithoutHeaderAsync("/api/auth/register");

        continued.Should()
            .BeFalse("FR-005: el usuario nace atado al tenant del header. Sin header no hay a donde crearlo");
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void TheExemptList_IsExactlyTheUnauthenticatedSurface()
    {
        TenantMiddleware.UnauthenticatedPaths
            .Should()
            .BeEquivalentTo(
                ["/api/auth/login", "/api/auth/confirm-device"],
                "T055a: agregar una ruta aca amplia la superficie sin autenticar. "
                + "Este test obliga a que sea un acto consciente");
    }
}
