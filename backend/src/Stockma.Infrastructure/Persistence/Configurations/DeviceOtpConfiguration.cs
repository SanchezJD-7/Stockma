using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Identity;

namespace Stockma.Infrastructure.Persistence.Configurations;

public sealed class DeviceOtpConfiguration : IEntityTypeConfiguration<DeviceOtp>
{
    public void Configure(EntityTypeBuilder<DeviceOtp> builder)
    {
        builder.ToTable("device_otps");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(o => o.TenantId).HasColumnName("tenant_id");
        builder.Property(o => o.UserId).HasColumnName("user_id");
        builder.Property(o => o.DeviceId).HasColumnName("device_id").HasMaxLength(128).IsRequired();
        builder.Property(o => o.CodeHash).HasColumnName("code_hash").HasMaxLength(256).IsRequired();
        builder.Property(o => o.IssuedAt).HasColumnName("issued_at");
        builder.Property(o => o.ExpiresAt).HasColumnName("expires_at");
        builder.Property(o => o.ConsumedAt).HasColumnName("consumed_at");

        builder
            .HasIndex(o => new { o.TenantId, o.UserId, o.DeviceId, o.ExpiresAt })
            .HasDatabaseName("ix_device_otps_tenant_user_device_expiration");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(o => o.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
