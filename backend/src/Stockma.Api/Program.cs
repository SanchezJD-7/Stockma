using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Stockma.Api;
using Stockma.Api.Cli;
using Stockma.Api.Middleware;
using Stockma.Application.Identity;
using Stockma.Application.DependencyInjection;
using Stockma.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // El contrato expone los enums como string ("Medication"), no como numero.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = context => ProblemResponses.ValidationFailed(context.ModelState));
builder.Services.AddOpenApi();
builder.Services
    .AddOptions<ForwardedHeadersOptions>()
    .Configure(options =>
        ForwardedHeadersSetup.Configure(options, builder.Configuration.GetSection(ForwardedHeadersSetup.SectionName)))
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection("Jwt");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Key"] ?? string.Empty)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = StockmaClaimTypes.Role,
        };
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(
        AuthorizationPolicies.TenantAdmin,
        policy => policy.RequireRole(TenantRoles.TenantAdmin));
});

var authRateLimit = builder.Configuration.GetSection("RateLimiting:Auth");
var authPermitLimit = authRateLimit.GetValue("PermitPerWindow", RateLimitPolicies.PermitPerWindow);
var authWindowSeconds = authRateLimit.GetValue("WindowSeconds", (int)RateLimitPolicies.Window.TotalSeconds);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancellationToken) => ProblemResponses.WriteRateLimitedAsync(context.HttpContext, cancellationToken);

    options.AddPolicy(
        RateLimitPolicies.Auth,
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueLimit = 0,
            }));
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSmsSender(builder.Environment.IsDevelopment());

if (RuntimeDatabaseRoleCheck.AppliesTo(builder.Environment))
{
    builder.Services.AddHostedService<RuntimeDatabaseRoleCheck>();
}

var app = builder.Build();

if (args.Length > 0 && string.Equals(args[0], BootstrapAdminCommandLine.CommandName, StringComparison.OrdinalIgnoreCase))
{
    return await BootstrapAdminCommandLine.RunAsync(args, app.Services, Console.Out, Console.Error);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<DomainExceptionMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();

app.UseMiddleware<TenantMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();

return 0;

public partial class Program;
