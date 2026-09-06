using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockma.Domain.Entities;

namespace Stockma.Infrastructure.Persistence.Configurations;

public sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("tenant_settings");
        builder.HasKey(s => s.TenantId);
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(s => s.MaxTrustedDevices).HasColumnName("max_trusted_devices");
        builder.Property(s => s.NextSkuNumber).HasColumnName("next_sku_number").HasDefaultValue(TenantSettings.InitialSkuNumber);
        builder.OwnsOne(s => s.Thresholds, thresholds =>
        {
            thresholds.Property(t => t.GreenMonths).HasColumnName("green_months");
            thresholds.Property(t => t.YellowMonths).HasColumnName("yellow_months");
        });

        builder.OwnsOne(s => s.Branding, branding =>
        {
            branding.Property(b => b.Primary).HasColumnName("branding_primary").HasMaxLength(7);
            branding.Property(b => b.PrimaryActive).HasColumnName("branding_primary_active").HasMaxLength(7);
            branding.Property(b => b.PrimaryBg).HasColumnName("branding_primary_bg").HasMaxLength(7);
        });
    }
}
