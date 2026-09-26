using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stockma.Application.Identity;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Identity;

public sealed class DeviceOtpService(
    StockmaDbContext context,
    ISmsSender smsSender,
    IOtpGenerator otpGenerator,
    IPasswordHasher<ApplicationUser> hasher,
    TimeProvider timeProvider) : IDeviceOtpService
{
    public static readonly TimeSpan Lifetime = DeviceOtp.MaxLifetime;

    public async Task IssueAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new PhoneNotEnrolledException();

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            throw new PhoneNotEnrolledException();
        }

        var code = otpGenerator.Generate();

        var otp = new DeviceOtp(
            context.CurrentTenantId,
            userId,
            deviceId,
            hasher.HashPassword(user, code),
            timeProvider.GetUtcNow(),
            Lifetime);

        context.DeviceOtps.Add(otp);
        await context.SaveChangesAsync(cancellationToken);
        await smsSender.SendAsync(
            user.PhoneNumber,
            $"Tu código de acceso a Stockma es {code}. Vence en {Lifetime.TotalMinutes:0} minutos.",
            cancellationToken);
    }

    public async Task ConsumeAsync(Guid userId, string deviceId, string code, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var candidates = await context.DeviceOtps
            .Where(otp => otp.UserId == userId
                && otp.DeviceId == deviceId
                && otp.ConsumedAt == null
                && otp.ExpiresAt > now)
            .OrderByDescending(otp => otp.IssuedAt)
            .ToListAsync(cancellationToken);

        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new OtpNotUsableException();
        }

        var match = candidates.FirstOrDefault(otp =>
            hasher.VerifyHashedPassword(user, otp.CodeHash, code) != PasswordVerificationResult.Failed);

        if (match is null)
        {
            throw new OtpNotUsableException();
        }

        match.Consume(now);
        await context.SaveChangesAsync(cancellationToken);
    }
}
