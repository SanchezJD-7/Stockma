using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Identity;

namespace Stockma.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.FamilyId).HasColumnName("family_id");
        builder.Property(t => t.DeviceId).HasColumnName("device_id").HasMaxLength(128).IsRequired();
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(t => t.IssuedAt).HasColumnName("issued_at");
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.FamilyExpiresAt).HasColumnName("family_expires_at");
        builder.Property(t => t.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(t => t.RevokedAt).HasColumnName("revoked_at");

        builder
            .HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_refresh_tokens_token_hash");

        builder
            .HasIndex(t => new { t.TenantId, t.FamilyId })
            .HasDatabaseName("ix_refresh_tokens_tenant_family");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
