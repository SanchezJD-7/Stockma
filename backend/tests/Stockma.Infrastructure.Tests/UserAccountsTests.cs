using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Identity;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class UserAccountsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid TenantA = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
    private static readonly Guid TenantB = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    private const string Password = "S3gura#2026Larga";

    private async Task<(UserAccounts Accounts, StockmaDbContext Context, TenantContext Tenant)> BuildAsync(
        Guid? activeTenant = null)
    {
        var tenant = new TenantContext();

        if (activeTenant is not null)
        {
            tenant.Set(activeTenant.Value);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(tenant);
        services.AddDbContext<StockmaDbContext>(options => options.UseNpgsql(postgres.ConnectionString));
        services.AddIdentityCore<ApplicationUser>(options => options.Password.RequiredLength = 12)
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<StockmaDbContext>();

        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<StockmaDbContext>();

        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO tenants (id) VALUES ({0}), ({1}) ON CONFLICT DO NOTHING;",
            TenantA,
            TenantB);

        var accounts = new UserAccounts(
            context,
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<ILookupNormalizer>(),
            provider.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
            new FixedTimeProvider(Now));

        return (accounts, context, tenant);
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@droga.co";

    [Fact]
    public async Task Create_ThenVerify_ResolvesTheTenantWithoutAnActiveTenantContext()
    {
        var (accounts, _, tenant) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        await accounts.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        var (login, _, _) = await BuildAsync(activeTenant: null);
        var identity = await login.VerifyCredentialsAsync(email, Password);

        identity.Should().NotBeNull("ADR-002: el login corre SIN contexto de tenant");
        identity!.TenantId.Should().Be(TenantA, "T055b: el tenant se resuelve desde el email");
        tenant.IsResolved.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_FindsAUserOfAnyTenant()
    {
        var (setup, _, _) = await BuildAsync(activeTenant: TenantB);
        var email = UniqueEmail();
        await setup.CreateAsync(new NewUser(TenantB, email, Password, "+573001234567", TenantRoles.Member));

        var (login, _, _) = await BuildAsync(activeTenant: null);

        (await login.VerifyCredentialsAsync(email, Password))!.TenantId.Should().Be(TenantB);
    }

    [Fact]
    public async Task Verify_ReturnsTheUsersRoles()
    {
        var (setup, _, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        await setup.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.TenantAdmin));

        var (login, _, _) = await BuildAsync(activeTenant: null);

        (await login.VerifyCredentialsAsync(email, Password))!.Roles
            .Should()
            .BeEquivalentTo([TenantRoles.TenantAdmin], "el claim role del JWT sale de acá");
    }

    // T055c — los tres caminos de falla devuelven null, y el handler no puede distinguirlos.

    [Fact]
    public async Task Verify_WithAnUnknownEmail_ReturnsNull()
    {
        var (accounts, _, _) = await BuildAsync(activeTenant: null);

        (await accounts.VerifyCredentialsAsync(UniqueEmail(), Password)).Should().BeNull();
    }

    [Fact]
    public async Task Verify_WithTheWrongPassword_ReturnsNull()
    {
        var (setup, _, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        await setup.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        var (login, _, _) = await BuildAsync(activeTenant: null);

        (await login.VerifyCredentialsAsync(email, "OtraClave#2026Larga")).Should().BeNull();
    }

    [Fact]
    public async Task Verify_IsCaseInsensitiveOnTheEmail()
    {
        var (setup, _, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        await setup.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        var (login, _, _) = await BuildAsync(activeTenant: null);

        (await login.VerifyCredentialsAsync(email.ToUpperInvariant(), Password))
            .Should()
            .NotBeNull("Identity normaliza el email a mayúsculas; el lookup usa el mismo normalizador");
    }

    [Fact]
    public async Task Verify_TrimsTheEmail()
    {
        var (setup, _, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        await setup.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        var (login, _, _) = await BuildAsync(activeTenant: null);

        (await login.VerifyCredentialsAsync($"  {email}  ", Password)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Verify_WithABlankEmail_ReturnsNullWithoutQuerying(string email)
    {
        var (accounts, _, _) = await BuildAsync(activeTenant: null);

        (await accounts.VerifyCredentialsAsync(email, Password)).Should().BeNull();
    }

    [Fact]
    public async Task Verify_ForALockedOutUser_ReturnsNull()
    {
        var (setup, context, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        var userId = await setup.CreateAsync(
            new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE users SET lockout_end = {0}, lockout_enabled = true WHERE id = {1};",
            Now.AddHours(1),
            userId);

        var (login, _, _) = await BuildAsync(activeTenant: null);

        (await login.VerifyCredentialsAsync(email, Password))
            .Should()
            .BeNull("T068 apoya el offboarding en el lockout de Identity");
    }

    [Fact]
    public async Task Verify_ForAUserWhoseLockoutExpired_Succeeds()
    {
        var (setup, context, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        var userId = await setup.CreateAsync(
            new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE users SET lockout_end = {0}, lockout_enabled = true WHERE id = {1};",
            Now.AddHours(-1),
            userId);

        var (login, _, _) = await BuildAsync(activeTenant: null);

        (await login.VerifyCredentialsAsync(email, Password)).Should().NotBeNull();
    }

    [Fact]
    public async Task EmailExists_SeesTheWholePlatform()
    {
        var (setup, _, _) = await BuildAsync(activeTenant: TenantB);
        var email = UniqueEmail();
        await setup.CreateAsync(new NewUser(TenantB, email, Password, "+573001234567", TenantRoles.Member));

        var (other, _, _) = await BuildAsync(activeTenant: TenantA);

        (await other.EmailExistsAsync(email))
            .Should()
            .BeTrue("ADR-001: el email es único en TODA la plataforma, no por tenant");
    }

    [Fact]
    public async Task FindByEmail_WorksWithoutAnActiveTenant()
    {
        var (setup, _, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        await setup.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        var (confirm, _, _) = await BuildAsync(activeTenant: null);

        (await confirm.FindByEmailAsync(email))!.TenantId
            .Should()
            .Be(TenantA, "confirm-device tampoco conoce su tenant: el cliente todavía no tiene token");
    }
}
