using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stockma.Application.Identity;
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
        public List<(string PhoneNumber, string Message)> Sent { get; } = [];

        public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        {
            Sent.Add((phoneNumber, message));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedOtpGenerator(string code) : IOtpGenerator
    {
        public string Generate() => code;
    }

    private async Task<(StockmaDbContext Context, Guid UserId)> ArrangeAsync(string? phoneNumber = PhoneNumber)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(TenantId);

        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        var context = new StockmaDbContext(options, tenantContext);
        await context.Database.MigrateAsync();

        var user = new ApplicationUser(TenantId, $"{Guid.NewGuid():N}@droga.co")
        {
            PhoneNumber = phoneNumber,
            NormalizedEmail = null,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        user.NormalizedEmail = user.Email!.ToUpperInvariant();
        user.NormalizedUserName = user.UserName!.ToUpperInvariant();

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO tenants (id) VALUES ({0}) ON CONFLICT DO NOTHING;",
            TenantId);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return (context, user.Id);
    }

    private static DeviceOtpService CreateService(
        StockmaDbContext context,
        ISmsSender sender,
        IOtpGenerator generator) =>
        new(context, sender, generator, new PasswordHasher<ApplicationUser>(), new FixedTimeProvider(Now));

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
    public async Task Consume_WithTheWrongCode_DoesNotBurnTheOtp()
    {
        var (context, userId) = await ArrangeAsync();
        var service = CreateService(context, new CapturingSmsSender(), new FixedOtpGenerator("483920"));
        await service.IssueAsync(userId, DeviceId);

        await Assert.ThrowsAsync<OtpNotUsableException>(
            () => service.ConsumeAsync(userId, DeviceId, "000000"));

        var act = () => service.ConsumeAsync(userId, DeviceId, "483920");

        await act.Should()
            .NotThrowAsync("un intento errado no debe invalidar el código; el tope de intentos es otra tarea");
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
}
