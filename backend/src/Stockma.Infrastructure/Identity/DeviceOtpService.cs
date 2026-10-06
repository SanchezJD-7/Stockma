using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stockma.Application.Identity;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Identity;

public sealed class DeviceOtpService : IDeviceOtpService
{
    public static readonly TimeSpan Lifetime = DeviceOtp.MaxLifetime;

    private const string LockScope = "device_otps";

    private readonly StockmaDbContext context;
    private readonly ISmsSender smsSender;
    private readonly IOtpGenerator otpGenerator;
    private readonly IPasswordHasher<ApplicationUser> hasher;
    private readonly TimeProvider timeProvider;
    private readonly OtpOptions options;
    private readonly ILogger<DeviceOtpService> logger;

    public DeviceOtpService(
        StockmaDbContext context,
        ISmsSender smsSender,
        IOtpGenerator otpGenerator,
        IPasswordHasher<ApplicationUser> hasher,
        TimeProvider timeProvider,
        IOptions<OtpOptions> options,
        ILogger<DeviceOtpService> logger)
    {
        this.context = context;
        this.smsSender = smsSender;
        this.otpGenerator = otpGenerator;
        this.hasher = hasher;
        this.timeProvider = timeProvider;
        this.options = options.Value;
        this.logger = logger;

        EnsurePositive(this.options.MaxAttempts, nameof(OtpOptions.MaxAttempts));
        EnsurePositive(this.options.MaxIssuesPerWindow, nameof(OtpOptions.MaxIssuesPerWindow));
        EnsurePositive(this.options.IssueWindowMinutes, nameof(OtpOptions.IssueWindowMinutes));
    }

    public async Task IssueAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        var normalizedDeviceId = DeviceIdentifier.Normalize(deviceId);

        var delivery = await context.RunLockedAsync(
            LockScope,
            userId,
            () => IssueLockedAsync(userId, normalizedDeviceId, cancellationToken),
            cancellationToken);

        if (delivery is null)
        {
            return;
        }

        try
        {
            await smsSender.SendAsync(
                delivery.Value.PhoneNumber,
                $"Tu código de acceso a Stockma es {delivery.Value.Code}. Vence en {Lifetime.TotalMinutes:0} minutos.",
                cancellationToken);
        }
        catch (SmsDeliveryException exception)
        {
            logger.LogError(
                exception,
                "Falló el envío del SMS de OTP para el usuario {UserId}: {Reason}",
                userId,
                exception.Message);
        }
    }

    public Task ConsumeAsync(Guid userId, string deviceId, string code, CancellationToken cancellationToken = default) =>
        ConsumeAsync(userId, deviceId, code, () => Task.FromResult(true), cancellationToken);

    public async Task<T> ConsumeAsync<T>(
        Guid userId,
        string deviceId,
        string code,
        Func<Task<T>> onConsumed,
        CancellationToken cancellationToken = default)
    {
        var normalizedDeviceId = DeviceIdentifier.Normalize(deviceId);

        var (consumed, result) = await context.RunLockedAsync(
            LockScope,
            userId,
            async () => await ConsumeLockedAsync(userId, normalizedDeviceId, code, cancellationToken)
                ? (true, await onConsumed())
                : (false, default(T)!),
            cancellationToken);

        if (!consumed)
        {
            throw new OtpNotUsableException();
        }

        return result;
    }

    private async Task<(string PhoneNumber, string Code)?> IssueLockedAsync(
        Guid userId,
        string deviceId,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new PhoneNotEnrolledException();

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            throw new PhoneNotEnrolledException();
        }

        var now = timeProvider.GetUtcNow();
        var windowStart = now.AddMinutes(-options.IssueWindowMinutes);

        var issuedInWindow = await context.DeviceOtps.CountAsync(
            otp => otp.UserId == userId && otp.IssuedAt > windowStart,
            cancellationToken);

        if (issuedInWindow >= options.MaxIssuesPerWindow)
        {
            logger.LogWarning(
                "Emisión de OTP omitida para el usuario {UserId}: alcanzó {MaxIssuesPerWindow} códigos en {IssueWindowMinutes} minutos.",
                userId,
                options.MaxIssuesPerWindow,
                options.IssueWindowMinutes);

            return null;
        }

        var previous = await LiveOtps(userId, deviceId, now).ToListAsync(cancellationToken);

        foreach (var stale in previous)
        {
            stale.Invalidate(now);
        }

        var code = otpGenerator.Generate();

        context.DeviceOtps.Add(new DeviceOtp(
            context.CurrentTenantId,
            userId,
            deviceId,
            hasher.HashPassword(user, code),
            now,
            Lifetime));

        await context.SaveChangesAsync(cancellationToken);

        return (user.PhoneNumber, code);
    }

    private async Task<bool> ConsumeLockedAsync(
        Guid userId,
        string deviceId,
        string code,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var otp = await LiveOtps(userId, deviceId, now)
            .OrderByDescending(candidate => candidate.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var user = otp is null
            ? null
            : await context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (otp is null || user is null)
        {
            logger.LogWarning(
                "Intento de OTP rechazado para el usuario {UserId}: no hay un código vigente.",
                userId);

            return false;
        }

        if (hasher.VerifyHashedPassword(user, otp.CodeHash, code) == PasswordVerificationResult.Failed)
        {
            otp.RegisterFailedAttempt(now, options.MaxAttempts);

            var recorded = await TrySaveAsync(cancellationToken);

            logger.LogWarning(
                "Intento de OTP rechazado para el usuario {UserId}: código incorrecto. Intentos fallidos {FailedAttempts} de {MaxAttempts}; quemado: {Burned}; registrado: {Recorded}.",
                userId,
                otp.FailedAttempts,
                options.MaxAttempts,
                otp.InvalidatedAt is not null,
                recorded);

            return false;
        }

        otp.Consume(now);

        return await TrySaveAsync(cancellationToken);
    }

    private IQueryable<DeviceOtp> LiveOtps(Guid userId, string deviceId, DateTimeOffset now) =>
        context.DeviceOtps.Where(otp => otp.UserId == userId
            && otp.DeviceId == deviceId
            && otp.ConsumedAt == null
            && otp.InvalidatedAt == null
            && otp.ExpiresAt > now);

    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static void EnsurePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, $"La opción Otp:{name} debe ser positiva.");
        }
    }
}
