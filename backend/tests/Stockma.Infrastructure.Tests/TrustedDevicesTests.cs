using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Infrastructure.Identity;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class TrustedDevicesTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    private const string Fingerprint = "fp";

    private async Task<(TrustedDevices Devices, StockmaDbContext Context, Guid TenantId, Guid UserId)> BuildAsync(
        int maxTrustedDevices = 2)
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        var context = new StockmaDbContext(options, tenantContext);
        await context.Database.MigrateAsync();

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO tenants (id) VALUES ({0}) ON CONFLICT DO NOTHING;

            INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number)
            VALUES ({0}, {1}, 6, 3, 1) ON CONFLICT (tenant_id) DO UPDATE SET max_trusted_devices = {1};

            INSERT INTO users (id, tenant_id, email, normalized_email, user_name, normalized_user_name,
                               email_confirmed, password_hash, security_stamp, concurrency_stamp,
                               phone_number, phone_number_confirmed, two_factor_enabled,
                               lockout_enabled, access_failed_count)
            VALUES ({2}, {0}, {3}, {4}, {3}, {4}, true, 'hash', 'stamp', gen_random_uuid()::text,
                    '+573001234567', false, false, true, 0);
            """,
            tenantId,
            maxTrustedDevices,
            userId,
            $"{userId:N}@droga.co",
            $"{userId:N}@DROGA.CO".ToUpperInvariant());

        return (new TrustedDevices(context, new FixedTimeProvider(Now)), context, tenantId, userId);
    }

    private static Task<bool> TrustAsync(TrustedDevices devices, Guid userId, string deviceId) =>
        devices.TryTrustAsync(userId, deviceId, Fingerprint);

    [Fact]
    public async Task TryTrust_TheFirstDevice_Succeeds()
    {
        var (devices, _, _, userId) = await BuildAsync();

        (await TrustAsync(devices, userId, "dev-1")).Should().BeTrue();
    }

    [Fact]
    public async Task TryTrust_UpToTheTenantsLimit_Succeeds()
    {
        var (devices, _, _, userId) = await BuildAsync(maxTrustedDevices: 2);

        (await TrustAsync(devices, userId, "dev-1")).Should().BeTrue();
        (await TrustAsync(devices, userId, "dev-2")).Should().BeTrue();
    }

    [Fact]
    public async Task TryTrust_BeyondTheLimit_ReturnsFalse()
    {
        var (devices, _, _, userId) = await BuildAsync(maxTrustedDevices: 2);
        await TrustAsync(devices, userId, "dev-1");
        await TrustAsync(devices, userId, "dev-2");

        (await TrustAsync(devices, userId, "dev-3"))
            .Should()
            .BeFalse("FR-007: sin slot libre el dispositivo NO queda trusted");
    }

    [Fact]
    public async Task TryTrust_BeyondTheLimit_DoesNotCreateTheRow()
    {
        var (devices, context, _, userId) = await BuildAsync(maxTrustedDevices: 2);
        await TrustAsync(devices, userId, "dev-1");
        await TrustAsync(devices, userId, "dev-2");
        await TrustAsync(devices, userId, "dev-3");

        var stored = await context.TrustedDevices
            .Where(device => device.UserId == userId)
            .Select(device => device.DeviceId)
            .ToListAsync();

        stored.Should().BeEquivalentTo(["dev-1", "dev-2"], "la fila NO se crea cuando ya se alcanzó el máximo");
    }

    [Fact]
    public async Task TryTrust_BeyondTheLimit_RevokesNobody()
    {
        var (devices, context, _, userId) = await BuildAsync(maxTrustedDevices: 2);
        await TrustAsync(devices, userId, "dev-1");
        await TrustAsync(devices, userId, "dev-2");
        await TrustAsync(devices, userId, "dev-3");

        (await context.TrustedDevices.CountAsync(device => device.RevokedAt != null))
            .Should()
            .Be(0, "FR-007: nadie es expulsado automáticamente y no hay selección del más antiguo");
    }

    [Fact]
    public async Task TryTrust_TheSameDeviceTwice_DoesNotConsumeASecondSlot()
    {
        var (devices, context, _, userId) = await BuildAsync(maxTrustedDevices: 2);
        await TrustAsync(devices, userId, "dev-1");

        (await TrustAsync(devices, userId, "dev-1")).Should().BeTrue();

        (await context.TrustedDevices.CountAsync(device => device.UserId == userId))
            .Should()
            .Be(1, "un dispositivo ya confiable no se duplica ni gasta otro slot");
    }

    [Fact]
    public async Task TryTrust_RespectsTheTenantsConfiguredLimit()
    {
        var (devices, _, _, userId) = await BuildAsync(maxTrustedDevices: 4);

        foreach (var index in Enumerable.Range(1, 4))
        {
            (await TrustAsync(devices, userId, $"dev-{index}")).Should().BeTrue();
        }

        (await TrustAsync(devices, userId, "dev-5"))
            .Should()
            .BeFalse("el límite sale de TenantSettings, no de una constante");
    }

    [Fact]
    public async Task TryTrust_SetsTheExpiryFromTheLifetime()
    {
        var (devices, context, _, userId) = await BuildAsync();
        await TrustAsync(devices, userId, "dev-1");

        var stored = await context.TrustedDevices.SingleAsync(device => device.UserId == userId);

        stored.TrustedAt.Should().Be(Now);
        stored.ExpiresAt.Should().Be(Now.Add(TrustedDevices.DefaultLifetime));
        stored.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task TryTrust_AnExpiredDevice_DoesNotConsumeASlot()
    {
        var (devices, context, tenantId, userId) = await BuildAsync(maxTrustedDevices: 1);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint, trusted_at, expires_at)
            VALUES (gen_random_uuid(), {0}, {1}, 'dev-viejo', 'fp', {2}, {3});
            """,
            tenantId,
            userId,
            Now.AddDays(-30),
            Now.AddDays(-1));

        (await TrustAsync(devices, userId, "dev-nuevo"))
            .Should()
            .BeTrue("ADR-007: el vencido deja de contar como activo y libera el slot solo");
    }

    [Fact]
    public async Task TryTrust_ARevokedDevice_DoesNotConsumeASlot()
    {
        var (devices, context, tenantId, userId) = await BuildAsync(maxTrustedDevices: 1);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint,
                                         trusted_at, expires_at, revoked_at)
            VALUES (gen_random_uuid(), {0}, {1}, 'dev-revocado', 'fp', {2}, {3}, {4});
            """,
            tenantId,
            userId,
            Now.AddDays(-1),
            Now.AddDays(14),
            Now);

        (await TrustAsync(devices, userId, "dev-nuevo"))
            .Should()
            .BeTrue("revocar libera el slot: es lo que hace útil el endpoint de T067");
    }

    [Fact]
    public async Task IsTrusted_ForAnUnknownDevice_IsFalse()
    {
        var (devices, _, _, userId) = await BuildAsync();

        (await devices.IsTrustedAsync(userId, "dev-desconocido")).Should().BeFalse();
    }

    [Fact]
    public async Task IsTrusted_ForATrustedDevice_IsTrue()
    {
        var (devices, _, _, userId) = await BuildAsync();
        await TrustAsync(devices, userId, "dev-1");

        (await devices.IsTrustedAsync(userId, "dev-1")).Should().BeTrue();
    }

    [Fact]
    public async Task IsTrusted_ForAnExpiredDevice_IsFalse()
    {
        var (devices, context, tenantId, userId) = await BuildAsync();

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint, trusted_at, expires_at)
            VALUES (gen_random_uuid(), {0}, {1}, 'dev-vencido', 'fp', {2}, {3});
            """,
            tenantId,
            userId,
            Now.AddDays(-30),
            Now.AddDays(-1));

        (await devices.IsTrustedAsync(userId, "dev-vencido"))
            .Should()
            .BeFalse("un dispositivo vencido vuelve a exigir el OTP");
    }

    [Fact]
    public async Task IsTrusted_ForARevokedDevice_IsFalse()
    {
        var (devices, context, tenantId, userId) = await BuildAsync();

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint,
                                         trusted_at, expires_at, revoked_at)
            VALUES (gen_random_uuid(), {0}, {1}, 'dev-revocado', 'fp', {2}, {3}, {4});
            """,
            tenantId,
            userId,
            Now.AddDays(-1),
            Now.AddDays(14),
            Now);

        (await devices.IsTrustedAsync(userId, "dev-revocado")).Should().BeFalse();
    }

    [Fact]
    public async Task TryTrust_AnotherUsersDevices_DoNotConsumeYourSlots()
    {
        var (devices, context, tenantId, userId) = await BuildAsync(maxTrustedDevices: 1);
        var otherUserId = Guid.CreateVersion7();

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO users (id, tenant_id, email, normalized_email, user_name, normalized_user_name,
                               email_confirmed, password_hash, security_stamp, concurrency_stamp,
                               phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES ({0}, {1}, {2}, {3}, {2}, {3}, true, 'hash', 'stamp', gen_random_uuid()::text,
                    false, false, true, 0);

            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint, trusted_at, expires_at)
            VALUES (gen_random_uuid(), {1}, {0}, 'dev-ajeno', 'fp', {4}, {5});
            """,
            otherUserId,
            tenantId,
            $"{otherUserId:N}@droga.co",
            $"{otherUserId:N}@DROGA.CO".ToUpperInvariant(),
            Now,
            Now.AddDays(14));

        (await TrustAsync(devices, userId, "dev-propio"))
            .Should()
            .BeTrue("FR-007: el límite es por usuario, no por tenant");
    }

    [Fact]
    public async Task TryTrust_ADeviceThatWasRevoked_CanBeTrustedAgain()
    {
        var (devices, context, tenantId, userId) = await BuildAsync(maxTrustedDevices: 2);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint,
                                         trusted_at, expires_at, revoked_at)
            VALUES (gen_random_uuid(), {0}, {1}, 'dev-laptop', 'fp', {2}, {3}, {4});
            """,
            tenantId,
            userId,
            Now.AddDays(-5),
            Now.AddDays(10),
            Now.AddDays(-1));

        (await TrustAsync(devices, userId, "dev-laptop"))
            .Should()
            .BeTrue("el admin revoco por error, o el empleado volvio: reconfiar el MISMO aparato debe poder");
    }

    [Fact]
    public async Task TryTrust_ADeviceThatWasRevoked_KeepsTheRevocationOnRecord()
    {
        var (devices, context, tenantId, userId) = await BuildAsync(maxTrustedDevices: 2);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint,
                                         trusted_at, expires_at, revoked_at)
            VALUES (gen_random_uuid(), {0}, {1}, 'dev-laptop', 'fp', {2}, {3}, {4});
            """,
            tenantId,
            userId,
            Now.AddDays(-5),
            Now.AddDays(10),
            Now.AddDays(-1));

        await TrustAsync(devices, userId, "dev-laptop");

        var rows = await context.TrustedDevices
            .Where(device => device.UserId == userId && device.DeviceId == "dev-laptop")
            .ToListAsync();

        rows.Should().HaveCount(2, "la revocacion no se borra: queda como fila historica");
        rows.Count(device => device.RevokedAt != null).Should().Be(1);
        rows.Count(device => device.RevokedAt == null).Should().Be(1);
    }

    [Fact]
    public async Task TryTrust_ADeviceThatExpired_CanBeTrustedAgain()
    {
        var (devices, context, tenantId, userId) = await BuildAsync(maxTrustedDevices: 2);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint, trusted_at, expires_at)
            VALUES (gen_random_uuid(), {0}, {1}, 'dev-turno', 'fp', {2}, {3});
            """,
            tenantId,
            userId,
            Now.AddDays(-30),
            Now.AddDays(-1));

        (await TrustAsync(devices, userId, "dev-turno"))
            .Should()
            .BeTrue("el mismo aparato vuelve a confiarse tras vencer: es el caso normal cada 15 dias");
    }
}
