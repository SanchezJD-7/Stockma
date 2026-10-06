using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stockma.Application.Identity;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.Identity;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class DeviceOtpServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid TenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

    private const string DeviceId = "device-mostrador";
    private const string PhoneNumber = "+573001234567";

    private sealed class CapturingSmsSender : ISmsSender
    {
        private readonly ConcurrentQueue<(string PhoneNumber, string Message)> sent = new();

        public List<(string PhoneNumber, string Message)> Sent => [.. sent];

        public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        {
            sent.Enqueue((phoneNumber, message));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingSmsSender : ISmsSender
    {
        public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default) =>
            throw new SmsDeliveryException("Twilio respondió 500 InternalServerError");
    }

    private sealed class FixedOtpGenerator(string code) : IOtpGenerator
    {
        public string Generate() => code;
    }

    private sealed class CountingPasswordHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasher<ApplicationUser> inner = new();
        private int verifications;

        public int Verifications => Volatile.Read(ref verifications);

        public string HashPassword(ApplicationUser user, string password) => inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(
            ApplicationUser user,
            string hashedPassword,
            string providedPassword)
        {
            Interlocked.Increment(ref verifications);
            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> entries = new();

        public List<(LogLevel Level, string Message)> Entries => [.. entries];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue((logLevel, formatter(state, exception)));
    }

    private static TenantContext ActiveTenant()
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(TenantId);
        return tenantContext;
    }

    private StockmaDbContext NewContext() => postgres.CreateAppUserContext(ActiveTenant());

    private async Task<(StockmaDbContext Context, Guid UserId)> ArrangeAsync(string? phoneNumber = PhoneNumber)
    {
        await using var seeding = new StockmaDbContext(
            new DbContextOptionsBuilder<StockmaDbContext>().UseNpgsql(postgres.ConnectionString).Options,
            ActiveTenant());

        var user = new ApplicationUser(TenantId, $"{Guid.NewGuid():N}@droga.co")
        {
            PhoneNumber = phoneNumber,
            NormalizedEmail = null,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        user.NormalizedEmail = user.Email!.ToUpperInvariant();
        user.NormalizedUserName = user.UserName!.ToUpperInvariant();

        await seeding.Database.ExecuteSqlRawAsync(
            "INSERT INTO tenants (id) VALUES ({0}) ON CONFLICT DO NOTHING;",
            TenantId);

        seeding.Users.Add(user);
        await seeding.SaveChangesAsync();

        return (NewContext(), user.Id);
    }

    private static DeviceOtpService CreateService(
        StockmaDbContext context,
        ISmsSender sender,
        IOtpGenerator generator,
        OtpOptions? options = null,
        IPasswordHasher<ApplicationUser>? hasher = null,
        DateTimeOffset? now = null,
        ILogger<DeviceOtpService>? logger = null) =>
        new(
            context,
            sender,
            generator,
            hasher ?? new PasswordHasher<ApplicationUser>(),
            new FixedTimeProvider(now ?? Now),
            Options.Create(options ?? new OtpOptions()),
            logger ?? new CapturingLogger<DeviceOtpService>());

    [Fact]
    public async Task TheServiceUnderTest_RunsAsTheRestrictedAppUser()
    {
        var (context, _) = await ArrangeAsync();

        var role = await context.Database.SqlQueryRaw<string>("SELECT current_user::text AS \"Value\"").SingleAsync();

        role.Should().Be("app_user", "ADR-017: los locks, el OTP y sus escrituras se prueban bajo RLS, no como superusuario");
    }

    [Fact]
    public async Task Issue_SendsTheCodeToTheUsersPhoneNumber()
    {
        var (context, userId) = await ArrangeAsync();
        var sender = new CapturingSmsSender();
        var service = CreateService(context, sender, new FixedOtpGenerator("483920"));

        await service.IssueAsync(userId, DeviceId);

        sender.Sent.Should().ContainSingle();
        sender.Sent[0].PhoneNumber
            .Should()
            .Be(PhoneNumber, "FR-008: el OTP viaja por SMS al PhoneNumber del usuario, nunca por otro canal");
        sender.Sent[0].Message.Should().Contain("483920");
    }

    [Fact]
    public async Task Issue_NeverPersistsTheCodeInPlainText()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));

        await service.IssueAsync(userId, DeviceId);

        var stored = await context.DeviceOtps.SingleAsync(otp => otp.UserId == userId);

        stored.CodeHash.Should().NotContain("483920", "NFR-004: el OTP se persiste hasheado");
        stored.CodeHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Issue_ExpiresWithinTenMinutes()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));

        await service.IssueAsync(userId, DeviceId);

        var stored = await context.DeviceOtps.SingleAsync(otp => otp.UserId == userId);

        stored.ExpiresAt.Should().Be(Now.AddMinutes(10), "NFR-004");
    }

    [Fact]
    public async Task Issue_ForAUserWithoutPhoneNumber_IsRejected()
    {
        var (context, userId) = await ArrangeAsync(phoneNumber: null);
        var sender = new CapturingSmsSender();
        var service = CreateService(context, sender, new FixedOtpGenerator("483920"));

        var act = () => service.IssueAsync(userId, DeviceId);

        await act.Should()
            .ThrowAsync<PhoneNotEnrolledException>(
                "ADR-004: sin número cargado no hay destino, y NO DEBE permitirse el ingreso salteando el 2FA");

        sender.Sent.Should().BeEmpty();
        (await context.DeviceOtps.CountAsync(otp => otp.UserId == userId))
            .Should()
            .Be(0, "no se emite un OTP que no puede llegar a ningún lado");
    }

    [Fact]
    public async Task Consume_WithTheRightCode_Succeeds()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);

        var act = () => service.ConsumeAsync(userId, DeviceId, "483920");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Consume_MarksTheOtpAsUsed()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);

        await service.ConsumeAsync(userId, DeviceId, "483920");

        var stored = await context.DeviceOtps.SingleAsync(otp => otp.UserId == userId);
        stored.ConsumedAt.Should().Be(Now, "el OTP usado se marca consumido, no se borra");
    }

    [Fact]
    public async Task Consume_TheSameCodeTwice_IsRejected()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);
        await service.ConsumeAsync(userId, DeviceId, "483920");

        var act = () => service.ConsumeAsync(userId, DeviceId, "483920");

        await act.Should().ThrowAsync<OtpNotUsableException>("un OTP es de un solo uso");
    }

    [Fact]
    public async Task Consume_WithTheWrongCode_IsRejected()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);

        var act = () => service.ConsumeAsync(userId, DeviceId, "000000");

        await act.Should().ThrowAsync<OtpNotUsableException>();
    }

    [Fact]
    public async Task Consume_WithTheWrongCode_RecordsTheFailedAttempt()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);

        await Assert.ThrowsAsync<OtpNotUsableException>(
            () => service.ConsumeAsync(userId, DeviceId, "000000"));

        await using var fresh = NewContext();
        var stored = await fresh.DeviceOtps.SingleAsync(otp => otp.UserId == userId);
        stored.FailedAttempts.Should().Be(1, "auth-api.md: el intento DEBE quedar registrado (FR-008)");
    }

    [Fact]
    public async Task Consume_WhenTheFollowUpFails_RollsTheConsumptionBack()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConsumeAsync<bool>(
            userId,
            DeviceId,
            "483920",
            () => throw new InvalidOperationException("falló confiar el dispositivo")));

        await using var fresh = NewContext();
        (await fresh.DeviceOtps.SingleAsync(otp => otp.UserId == userId)).ConsumedAt
            .Should()
            .BeNull("ADR-017: si lo que sigue al consumo falla, el OTP no queda gastado sin JWT");

        var retry = () => CreateService(fresh, new CapturingSmsSender(), new FixedOtpGenerator("483920"))
            .ConsumeAsync(userId, DeviceId, "483920");
        await retry.Should().NotThrowAsync("el usuario reintenta con el mismo código");
    }

    [Fact]
    public async Task Consume_WhenTheFollowUpFails_RollsBackTheFollowUpsWritesToo()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);
        var devices = new TrustedDevices(context, new FixedTimeProvider(Now), new RefreshTokens(context));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConsumeAsync<bool>(
            userId,
            DeviceId,
            "483920",
            async () =>
            {
                await devices.TryTrustAsync(userId, DeviceId, "fp");
                throw new InvalidOperationException("falló emitir el token");
            }));

        await using var fresh = NewContext();
        (await fresh.TrustedDevices.CountAsync(device => device.UserId == userId))
            .Should()
            .Be(0, "consumo y confianza son una sola transacción: todo o nada");
    }

    [Fact]
    public async Task Consume_WithTheWrongCode_NeverRunsTheFollowUp_AndStillRecordsTheAttempt()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);
        var followUpRan = false;

        await Assert.ThrowsAsync<OtpNotUsableException>(() => service.ConsumeAsync(
            userId,
            DeviceId,
            "000000",
            () =>
            {
                followUpRan = true;
                return Task.FromResult(true);
            }));

        followUpRan.Should().BeFalse();
        await using var fresh = NewContext();
        (await fresh.DeviceOtps.SingleAsync(otp => otp.UserId == userId)).FailedAttempts
            .Should()
            .Be(1, "T075: el intento fallido se persiste aunque la operación entera falle");
    }

    [Fact]
    public async Task Consume_WithTheWrongCode_BelowTheLimit_KeepsTheOtpUsable()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);

        await Assert.ThrowsAsync<OtpNotUsableException>(
            () => service.ConsumeAsync(userId, DeviceId, "000000"));

        var act = () => service.ConsumeAsync(userId, DeviceId, "483920");

        await act.Should().NotThrowAsync("un typo por debajo del tope no obliga a pedir otro SMS");
    }

    [Fact]
    public async Task Consume_ReachingTheAttemptLimit_BurnsTheOtp()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(
            context,
            new CapturingSmsSender(),
            new FixedOtpGenerator("483920"),
            new OtpOptions { MaxAttempts = 3 });
        await service.IssueAsync(userId, DeviceId);

        foreach (var _ in Enumerable.Range(0, 3))
        {
            await Assert.ThrowsAsync<OtpNotUsableException>(
                () => service.ConsumeAsync(userId, DeviceId, "000000"));
        }

        var act = () => service.ConsumeAsync(userId, DeviceId, "483920");

        await act.Should()
            .ThrowAsync<OtpNotUsableException>("ADR-016: agotado el tope, ni el código correcto sirve");
    }

    [Fact]
    public async Task Consume_AfterTheAttemptLimit_StopsCheckingGuesses()
    {
        var (context, userId) = await ArrangeAsync();
        var hasher = new CountingPasswordHasher();
        var service = CreateService(
            context,
            new CapturingSmsSender(),
            new FixedOtpGenerator("483920"),
            new OtpOptions { MaxAttempts = 2 },
            hasher);
        await service.IssueAsync(userId, DeviceId);

        foreach (var _ in Enumerable.Range(0, 4))
        {
            await Assert.ThrowsAsync<OtpNotUsableException>(
                () => service.ConsumeAsync(userId, DeviceId, "000000"));
        }

        hasher.Verifications.Should().Be(2, "un OTP quemado no se compara contra nada: no hay oráculo");
    }

    [Fact]
    public async Task Consume_ParallelWrongGuesses_NeverEvaluateMoreThanTheLimit()
    {
        var (context, userId) = await ArrangeAsync();
        var hasher = new CountingPasswordHasher();
        var options = new OtpOptions { MaxAttempts = 3 };
        await CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"), options, hasher)
            .IssueAsync(userId, DeviceId);

        var guesses = Enumerable.Range(0, 8).Select(async index =>
        {
            await using var parallel = NewContext();
            var service = CreateService(
                parallel,
                new CapturingSmsSender(),
                new FixedOtpGenerator("483920"),
                options,
                hasher);

            try
            {
                await service.ConsumeAsync(userId, DeviceId, $"00000{index}");
            }
            catch (OtpNotUsableException)
            {
            }
        });

        await Task.WhenAll(guesses);

        hasher.Verifications.Should().Be(
            3,
            "ADR-016: los intentos se serializan por usuario; una ráfaga en paralelo no multiplica los intentos");

        await using var fresh = NewContext();
        var stored = await fresh.DeviceOtps.SingleAsync(otp => otp.UserId == userId);
        stored.FailedAttempts.Should().Be(3);
        stored.InvalidatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Consume_TheRightCodeTwiceInParallel_SucceedsExactlyOnce()
    {
        var (context, userId) = await ArrangeAsync();
        await CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"))
            .IssueAsync(userId, DeviceId);

        async Task<bool> ConsumeAsync()
        {
            await using var parallel = NewContext();
            var service = CreateService(parallel, new CapturingSmsSender(), new FixedOtpGenerator("483920"));

            try
            {
                await service.ConsumeAsync(userId, DeviceId, "483920");
                return true;
            }
            catch (OtpNotUsableException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(ConsumeAsync(), ConsumeAsync());

        results.Count(succeeded => succeeded)
            .Should()
            .Be(1, "ADR-016: dos confirm-device en carrera no pueden gastar el mismo OTP dos veces");
    }

    [Fact]
    public async Task Consume_AFailedAttempt_IsLoggedWithoutTheCodeNorThePhone()
    {
        var (context, userId) = await ArrangeAsync();
        var logger = new CapturingLogger<DeviceOtpService>();
        var service = CreateService(
            context,
            new CapturingSmsSender(),
            new FixedOtpGenerator("483920"),
            logger: logger);
        await service.IssueAsync(userId, DeviceId);

        await Assert.ThrowsAsync<OtpNotUsableException>(
            () => service.ConsumeAsync(userId, DeviceId, "000111"));

        var warnings = logger.Entries.Where(entry => entry.Level == LogLevel.Warning).ToList();
        warnings.Should().ContainSingle("auth-api.md: el intento fallido DEBE quedar registrado");
        warnings[0].Message.Should().Contain(userId.ToString());
        warnings[0].Message.Should().NotContain("000111").And.NotContain("483920").And.NotContain(PhoneNumber);
    }

    [Fact]
    public async Task Issue_InvalidatesThePreviousLiveOtpsOfThatDevice()
    {
        var (context, userId) = await ArrangeAsync();
        await CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("111111"))
            .IssueAsync(userId, DeviceId);
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("222222"));
        await service.IssueAsync(userId, DeviceId);

        var stale = () => service.ConsumeAsync(userId, DeviceId, "111111");

        await stale.Should().ThrowAsync<OtpNotUsableException>("ADR-016: sólo el último código emitido vale");

        var fresh = () => service.ConsumeAsync(userId, DeviceId, "222222");

        await fresh.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Consume_OnlyChecksTheNewestLiveOtp()
    {
        var (context, userId) = await ArrangeAsync();
        var hasher = new CountingPasswordHasher();
        var user = await context.Users.SingleAsync(u => u.Id == userId);

        context.DeviceOtps.Add(new DeviceOtp(
            TenantId,
            userId,
            DeviceId,
            hasher.HashPassword(user, "111111"),
            Now.AddMinutes(-2),
            DeviceOtp.MaxLifetime));
        context.DeviceOtps.Add(new DeviceOtp(
            TenantId,
            userId,
            DeviceId,
            hasher.HashPassword(user, "222222"),
            Now.AddMinutes(-1),
            DeviceOtp.MaxLifetime));
        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            new CapturingSmsSender(),
            new FixedOtpGenerator("333333"),
            hasher: hasher);

        await Assert.ThrowsAsync<OtpNotUsableException>(() => service.ConsumeAsync(userId, DeviceId, "111111"));

        hasher.Verifications.Should().Be(1, "cada intento se compara contra UN solo código, no contra todos los vivos");
    }

    [Fact]
    public async Task Issue_BeyondTheWindowCap_SendsNoSmsAndDoesNotFail()
    {
        var (context, userId) = await ArrangeAsync();
        var sender = new CapturingSmsSender();
        var options = new OtpOptions { MaxIssuesPerWindow = 2, IssueWindowMinutes = 15 };
        var service = CreateService(context, sender, new FixedOtpGenerator("483920"), options);

        await service.IssueAsync(userId, DeviceId);
        await service.IssueAsync(userId, DeviceId);

        var third = () => service.IssueAsync(userId, DeviceId);

        await third.Should().NotThrowAsync("ADR-016: pasado el tope el login responde igual, para no enumerar");
        sender.Sent.Should().HaveCount(2, "ADR-016: pasado el tope por ventana no se manda otro SMS");
        (await context.DeviceOtps.CountAsync(otp => otp.UserId == userId)).Should().Be(2);
    }

    [Fact]
    public async Task Issue_BeyondTheWindowCap_KeepsTheLastCodeUsable()
    {
        var (context, userId) = await ArrangeAsync();
        var options = new OtpOptions { MaxIssuesPerWindow = 1 };
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"), options);

        await service.IssueAsync(userId, DeviceId);
        await service.IssueAsync(userId, DeviceId);

        var act = () => service.ConsumeAsync(userId, DeviceId, "483920");

        await act.Should().NotThrowAsync("un pedido sobre el tope no invalida el código que ya le llegó");
    }

    [Fact]
    public async Task Issue_OutsideTheWindow_DoesNotCountTowardsTheCap()
    {
        var (context, userId) = await ArrangeAsync();
        var sender = new CapturingSmsSender();
        var options = new OtpOptions { MaxIssuesPerWindow = 1, IssueWindowMinutes = 15 };

        await CreateService(context, sender, new FixedOtpGenerator("111111"), options, now: Now.AddMinutes(-16))
            .IssueAsync(userId, DeviceId);
        await CreateService(context, sender, new FixedOtpGenerator("222222"), options)
            .IssueAsync(userId, DeviceId);

        sender.Sent.Should().HaveCount(2, "la ventana es deslizante: lo emitido hace más de 15 min ya no cuenta");
    }

    [Fact]
    public async Task Issue_ParallelRequests_NeverExceedTheWindowCap()
    {
        var (context, userId) = await ArrangeAsync();
        var sender = new CapturingSmsSender();
        var options = new OtpOptions { MaxIssuesPerWindow = 2 };

        var requests = Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var parallel = NewContext();
            await CreateService(parallel, sender, new FixedOtpGenerator("483920"), options)
                .IssueAsync(userId, DeviceId);
        });

        await Task.WhenAll(requests);

        sender.Sent.Should().HaveCount(2, "ADR-016: el tope se cuenta bajo el mismo lock que la emisión");
        (await context.DeviceOtps.CountAsync(otp => otp.UserId == userId && otp.InvalidatedAt == null))
            .Should()
            .Be(1, "a lo sumo un código vivo por dispositivo");
    }

    [Fact]
    public async Task Consume_NormalizesTheDeviceIdLikeIssue()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, $"  {DeviceId}  ");

        var act = () => service.ConsumeAsync(userId, DeviceId, "483920");

        await act.Should().NotThrowAsync("ADR-016: el deviceId se normaliza en un solo lugar para emitir y consumir");
    }

    [Fact]
    public async Task Consume_ForAnotherDevice_IsRejected()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);

        var act = () => service.ConsumeAsync(userId, "otro-dispositivo", "483920");

        await act.Should()
            .ThrowAsync<OtpNotUsableException>(
                "el OTP está atado al dispositivo que lo pidió: no sirve para confiar otro");
    }

    [Fact]
    public async Task Consume_WithNoOtpIssued_IsRejectedWithTheSameError()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));

        var act = () => service.ConsumeAsync(userId, DeviceId, "483920");

        await act.Should()
            .ThrowAsync<OtpNotUsableException>(
                "mismo error que un código errado: distinguirlos diría si el OTP existía");
    }

    [Fact]
    public async Task Issue_WhenTheProviderFails_DoesNotFailTheLogin()
    {
        var (context, userId) = await ArrangeAsync();
        var logger = new CapturingLogger<DeviceOtpService>();
        var service = CreateService(context, new FailingSmsSender(), new FixedOtpGenerator("483920"), logger: logger);

        var act = () => service.IssueAsync(userId, DeviceId);

        await act.Should()
            .NotThrowAsync(
                "ADR-016: si el proveedor de SMS cae, el login DEBE responder igual que con un envío real");
        logger.Entries.Should()
            .Contain(entry => entry.Level == LogLevel.Error, "el fallo sólo se entera el log");
        (await context.DeviceOtps.CountAsync(otp => otp.UserId == userId))
            .Should()
            .Be(1, "el código emitido queda vivo: lo invalida el próximo pedido y la expiración");
    }

    [Fact]
    public async Task Issue_WhenTheProviderFails_LogsTheReasonWithoutTheCodeNorThePhone()
    {
        var (context, userId) = await ArrangeAsync();
        var logger = new CapturingLogger<DeviceOtpService>();
        var service = CreateService(context, new FailingSmsSender(), new FixedOtpGenerator("483920"), logger: logger);

        await service.IssueAsync(userId, DeviceId);

        var error = logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error).Subject;

        error.Message.Should()
            .Contain(userId.ToString(), "el error tiene que permitir cruzar con el usuario")
            .And.NotContain("483920", "el log no guarda el código")
            .And.NotContain(PhoneNumber, "el log no guarda el celular");
    }
}
