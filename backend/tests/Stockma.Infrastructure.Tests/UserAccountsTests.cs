using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.Identity;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Persistence.Interceptors;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class UserAccountsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid TenantA = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
    private static readonly Guid TenantB = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    private const string Password = "S3gura#2026Larga";

    private sealed class CountingPasswordHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasher<ApplicationUser> inner = new();

        public int Verifications { get; private set; }

        public string HashPassword(ApplicationUser user, string password) => inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(
            ApplicationUser user,
            string hashedPassword,
            string providedPassword)
        {
            Verifications++;
            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    private async Task<(UserAccounts Accounts, StockmaDbContext Context, TenantContext Tenant)> BuildAsync(
        Guid? activeTenant = null,
        IPasswordHasher<ApplicationUser>? hasher = null)
    {
        var tenant = new TenantContext();

        if (activeTenant is not null)
        {
            tenant.Set(activeTenant.Value);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(tenant);
        services.AddDbContext<StockmaDbContext>(options => options
            .UseNpgsql(postgres.AppUserConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(tenant)));
        services.AddIdentityCore<ApplicationUser>(options => options.Password.RequiredLength = 12)
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<StockmaDbContext>();

        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO tenants (id) VALUES ({0}), ({1}) ON CONFLICT DO NOTHING;",
            TenantA,
            TenantB);

        var accounts = new UserAccounts(
            context,
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<ILookupNormalizer>(),
            hasher ?? provider.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
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
    public async Task FindByEmail_ForALockedOutUser_ReturnsNull()
    {
        var (setup, context, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        var userId = await setup.CreateAsync(
            new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE users SET lockout_end = {0}, lockout_enabled = true WHERE id = {1};",
            Now.AddHours(1),
            userId);

        var (confirm, _, _) = await BuildAsync(activeTenant: null);

        (await confirm.FindByEmailAsync(email))
            .Should()
            .BeNull("confirm-device aplica la misma regla de bloqueo que el login: deshabilitado no recibe JWT");
    }

    [Fact]
    public async Task FindByEmail_ForAUserWhoseLockoutExpired_FindsTheUser()
    {
        var (setup, context, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        var userId = await setup.CreateAsync(
            new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE users SET lockout_end = {0}, lockout_enabled = true WHERE id = {1};",
            Now.AddHours(-1),
            userId);

        var (confirm, _, _) = await BuildAsync(activeTenant: null);

        (await confirm.FindByEmailAsync(email)).Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WhenTheRoleCannotBeAssigned_LeavesNoUserBehind()
    {
        var (accounts, _, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();

        var act = () => accounts.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", "RolInexistente"));

        await act.Should().ThrowAsync<InvalidOperationException>();

        var (probe, _, _) = await BuildAsync(activeTenant: null);
        (await probe.EmailExistsAsync(email))
            .Should()
            .BeFalse("ADR-017: usuario y rol se crean en una transacción; un usuario sin rol dejaría el bootstrap trabado");
    }

    [Fact]
    public async Task Create_WhenAnotherTenantRegisteredTheEmailFirst_ThrowsEmailAlreadyRegistered()
    {
        var email = UniqueEmail();
        var (first, _, _) = await BuildAsync(activeTenant: TenantA);
        await first.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        var (second, _, _) = await BuildAsync(activeTenant: TenantB);
        var act = () => second.CreateAsync(new NewUser(TenantB, email, Password, "+573001234567", TenantRoles.Member));

        await act.Should().ThrowAsync<EmailAlreadyRegisteredException>(
            "ADR-001: la carrera entre tenants pasa el pre-chequeo y choca con el índice único; es un 409, no un 500");
    }

    [Fact]
    public async Task Create_WhenTheSameTenantAlreadyHasTheEmail_ThrowsEmailAlreadyRegistered()
    {
        var email = UniqueEmail();
        var (accounts, _, _) = await BuildAsync(activeTenant: TenantA);
        await accounts.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        var (again, _, _) = await BuildAsync(activeTenant: TenantA);
        var act = () => again.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        await act.Should().ThrowAsync<EmailAlreadyRegisteredException>();
    }

    [Fact]
    public async Task TenantHasAnyUser_WithoutUsers_IsFalse()
    {
        var emptyTenant = Guid.NewGuid();
        var (accounts, context, _) = await BuildAsync(activeTenant: emptyTenant);
        await context.Database.ExecuteSqlRawAsync("INSERT INTO tenants (id) VALUES ({0});", emptyTenant);

        (await accounts.TenantHasAnyUserAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task TenantHasAnyUser_WithAUser_IsTrue()
    {
        var (accounts, _, _) = await BuildAsync(activeTenant: TenantA);
        await accounts.CreateAsync(new NewUser(TenantA, UniqueEmail(), Password, "+573001234567", TenantRoles.Member));

        (await accounts.TenantHasAnyUserAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task TenantHasAnyUser_IgnoresUsersOfOtherTenants()
    {
        var (setup, _, _) = await BuildAsync(activeTenant: TenantB);
        await setup.CreateAsync(new NewUser(TenantB, UniqueEmail(), Password, "+573001234567", TenantRoles.Member));

        var emptyTenant = Guid.NewGuid();
        var (accounts, context, _) = await BuildAsync(activeTenant: emptyTenant);
        await context.Database.ExecuteSqlRawAsync("INSERT INTO tenants (id) VALUES ({0});", emptyTenant);

        (await accounts.TenantHasAnyUserAsync())
            .Should()
            .BeFalse("el bootstrap de un tenant vacío no puede trabarse por los usuarios de otro");
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
    [Fact]
    public async Task Verify_WithAnUnknownEmail_StillRunsOnePasswordVerification()
    {
        var hasher = new CountingPasswordHasher();
        var (accounts, _, _) = await BuildAsync(activeTenant: null, hasher: hasher);

        await accounts.VerifyCredentialsAsync(UniqueEmail(), Password);

        hasher.Verifications.Should().Be(
            1,
            "ADR-016: sin PBKDF2 el email inexistente responde más rápido y el tiempo enumera correos");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Verify_WithABlankEmail_StillRunsOnePasswordVerification(string email)
    {
        var hasher = new CountingPasswordHasher();
        var (accounts, _, _) = await BuildAsync(activeTenant: null, hasher: hasher);

        await accounts.VerifyCredentialsAsync(email, Password);

        hasher.Verifications.Should().Be(1);
    }

    [Fact]
    public async Task Verify_ForALockedOutUser_StillRunsOnePasswordVerification()
    {
        var (setup, context, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        var userId = await setup.CreateAsync(
            new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE users SET lockout_end = {0}, lockout_enabled = true WHERE id = {1};",
            Now.AddHours(1),
            userId);

        var hasher = new CountingPasswordHasher();
        var (login, _, _) = await BuildAsync(activeTenant: null, hasher: hasher);

        (await login.VerifyCredentialsAsync(email, Password)).Should().BeNull();
        hasher.Verifications.Should().Be(1, "ADR-016: el usuario bloqueado tampoco puede responder más rápido");
    }

    [Fact]
    public async Task Verify_WithTheWrongPassword_RunsExactlyOnePasswordVerification()
    {
        var (setup, _, _) = await BuildAsync(activeTenant: TenantA);
        var email = UniqueEmail();
        await setup.CreateAsync(new NewUser(TenantA, email, Password, "+573001234567", TenantRoles.Member));

        var hasher = new CountingPasswordHasher();
        var (login, _, _) = await BuildAsync(activeTenant: null, hasher: hasher);

        await login.VerifyCredentialsAsync(email, "OtraClave#2026Larga");

        hasher.Verifications.Should().Be(1, "mismo costo que el camino del email inexistente");
    }

    [Fact]
    public async Task Create_WithAPasswordIdentityRejects_IsAValidationFailure()
    {
        var (accounts, _, _) = await BuildAsync(activeTenant: TenantA);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => accounts.CreateAsync(new NewUser(TenantA, UniqueEmail(), "corta", "+573001234567", TenantRoles.Member)));

        exception.ErrorCode.Should().Be("VALIDATION_FAILED", "auth-api.md: 400, no un 500");
    }
}
