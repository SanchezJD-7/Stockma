using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Identity;

namespace Stockma.Infrastructure.Persistence.Configurations;

public sealed class TrustedDeviceConfiguration : IEntityTypeConfiguration<TrustedDevice>
{
    public void Configure(EntityTypeBuilder<TrustedDevice> builder)
    {
        builder.ToTable("trusted_devices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.UserId).HasColumnName("user_id");
        builder.Property(d => d.DeviceId).HasColumnName("device_id").HasMaxLength(128).IsRequired();
        builder.Property(d => d.Fingerprint).HasColumnName("fingerprint").HasMaxLength(256).IsRequired();
        builder.Property(d => d.TrustedAt).HasColumnName("trusted_at");
        builder.Property(d => d.ExpiresAt).HasColumnName("expires_at");
        builder.Property(d => d.RevokedAt).HasColumnName("revoked_at");

        builder
            .HasIndex(d => new { d.TenantId, d.UserId, d.DeviceId })
            .IsUnique()
            .HasDatabaseName("ux_trusted_devices_tenant_user_device");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
