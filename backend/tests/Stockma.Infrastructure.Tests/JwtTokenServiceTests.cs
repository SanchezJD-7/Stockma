using System.IdentityModel.Tokens.Jwt;

using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Identity;

namespace Stockma.Infrastructure.Tests;

public class JwtTokenServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    private const string SigningKey = "clave-de-firma-de-al-menos-32-bytes-para-hmac-sha256";

    private static JwtOptions Options(int expiresMinutes = 60) => new()
    {
        Issuer = "stockma",
        Audience = "stockma-api",
        Key = SigningKey,
        ExpiresMinutes = expiresMinutes,
    };

    private static JwtTokenService CreateService(JwtOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? Options()), new FixedTimeProvider(Now));

    private static JwtSecurityToken Decode(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static string? ClaimOf(string token, string type) =>
        Decode(token).Claims.FirstOrDefault(claim => claim.Type == type)?.Value;

    [Fact]
    public void Create_PutsTheUserIdInSub()
    {
        var token = CreateService().Create(UserId, TenantId, [TenantRoles.Member]);

        ClaimOf(token.Value, JwtRegisteredClaimNames.Sub)
            .Should()
            .Be(UserId.ToString(), "FR-006: el JWT lleva el claim sub");
    }

    [Fact]
    public void Create_PutsTheTenantIdInTid()
    {
        var token = CreateService().Create(UserId, TenantId, [TenantRoles.Member]);

        ClaimOf(token.Value, StockmaClaimTypes.TenantId)
            .Should()
            .Be(TenantId.ToString(), "FR-006: el JWT lleva el claim tid");
    }

    [Fact]
    public void Create_PutsEveryRoleInTheToken()
    {
        var token = CreateService().Create(UserId, TenantId, [TenantRoles.TenantAdmin, TenantRoles.Member]);

        Decode(token.Value)
            .Claims.Where(claim => claim.Type == StockmaClaimTypes.Role)
            .Select(claim => claim.Value)
            .Should()
            .BeEquivalentTo(
                [TenantRoles.TenantAdmin, TenantRoles.Member],
                "T062: la policy de /api/admin/* se resuelve con el claim role del JWT");
    }

    [Fact]
    public void Create_ExpiresWithinSixtyMinutes()
    {
        var token = CreateService().Create(UserId, TenantId, [TenantRoles.Member]);

        Decode(token.Value)
            .ValidTo
            .Should()
            .BeOnOrBefore(Now.AddMinutes(60).UtcDateTime, "NFR-004: el JWT DEBE expirar en <= 60 min");
    }

    [Fact]
    public void Create_ReportsExpiryInSecondsWithinTheContractCap()
    {
        var token = CreateService().Create(UserId, TenantId, [TenantRoles.Member]);

        token.ExpiresInSeconds
            .Should()
            .BePositive()
            .And.BeLessThanOrEqualTo(3600, "contrato: expiresIn <= 3600");
    }

    [Fact]
    public void Create_UsesTheConfiguredIssuerAndAudience()
    {
        var token = CreateService().Create(UserId, TenantId, [TenantRoles.Member]);
        var decoded = Decode(token.Value);

        decoded.Issuer.Should().Be("stockma");
        decoded.Audiences.Should().Contain("stockma-api");
    }

    [Fact]
    public void Create_SignsWithTheConfiguredKey()
    {
        var token = CreateService().Create(UserId, TenantId, [TenantRoles.Member]);

        var act = () => new JwtSecurityTokenHandler().ValidateToken(
            token.Value,
            new TokenValidationParameters
            {
                ValidIssuer = "stockma",
                ValidAudience = "stockma-api",
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(SigningKey)),
                ClockSkew = TimeSpan.Zero,
                LifetimeValidator = (_, expires, _, _) => expires > Now.UtcDateTime,
            },
            out _);

        act.Should().NotThrow();
    }

    [Fact]
    public void Create_TokenSignedWithAnotherKey_DoesNotValidate()
    {
        var token = CreateService().Create(UserId, TenantId, [TenantRoles.Member]);

        var act = () => new JwtSecurityTokenHandler().ValidateToken(
            token.Value,
            new TokenValidationParameters
            {
                ValidIssuer = "stockma",
                ValidAudience = "stockma-api",
                IssuerSigningKey = new SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes("otra-clave-distinta-de-al-menos-32-bytes!!")),
                ClockSkew = TimeSpan.Zero,
                LifetimeValidator = (_, expires, _, _) => expires > Now.UtcDateTime,
            },
            out _);

        act.Should().Throw<SecurityTokenSignatureKeyNotFoundException>();
    }

    [Fact]
    public void Service_RejectsAnExpiryBeyondTheNfrCap()
    {
        var act = () => CreateService(Options(expiresMinutes: 61));

        act.Should()
            .Throw<ArgumentOutOfRangeException>("NFR-004 es un techo, no una sugerencia")
            .WithParameterName("ExpiresMinutes");
    }

    [Fact]
    public void Service_RejectsANonPositiveExpiry()
    {
        var act = () => CreateService(Options(expiresMinutes: 0));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("ExpiresMinutes");
    }

    [Fact]
    public void Service_RejectsAKeyTooShortForHmacSha256()
    {
        var options = Options();
        options.Key = "corta";

        var act = () => CreateService(options);

        act.Should()
            .Throw<ArgumentException>("HMAC-SHA256 exige una clave de al menos 256 bits")
            .WithParameterName("Key");
    }

    [Fact]
    public void Create_RejectsAnEmptyUserId()
    {
        var act = () => CreateService().Create(Guid.Empty, TenantId, [TenantRoles.Member]);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public void Create_RejectsAnEmptyTenantId()
    {
        var act = () => CreateService().Create(UserId, Guid.Empty, [TenantRoles.Member]);

        act.Should()
            .Throw<ArgumentException>("un JWT sin tid dejaría al portador fuera de todo aislamiento")
            .WithParameterName("tenantId");
    }

    [Fact]
    public void Create_GivesEveryTokenItsOwnJti()
    {
        var service = CreateService();

        var first = ClaimOf(service.Create(UserId, TenantId, [TenantRoles.Member]).Value, JwtRegisteredClaimNames.Jti);
        var second = ClaimOf(service.Create(UserId, TenantId, [TenantRoles.Member]).Value, JwtRegisteredClaimNames.Jti);

        first.Should().NotBeNullOrEmpty();
        first.Should().NotBe(second, "cada token debe ser identificable para poder revocarlo");
    }
}
