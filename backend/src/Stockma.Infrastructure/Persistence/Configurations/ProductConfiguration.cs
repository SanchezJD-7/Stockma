using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockma.Domain.Entities;

namespace Stockma.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.Sku).HasColumnName("sku").HasMaxLength(64).IsRequired();
        builder.Property(p => p.Barcode).HasColumnName("barcode").HasMaxLength(64);
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(p => p.Category).HasColumnName("category").HasConversion<int>();
        builder.Property(p => p.ActiveIngredient).HasColumnName("active_ingredient").HasMaxLength(256);
        builder.Property(p => p.Presentation).HasColumnName("presentation").HasMaxLength(128);
        builder.Property(p => p.StorageConditions).HasColumnName("storage_conditions").HasMaxLength(256);
        builder.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.HasIndex(p => new { p.TenantId, p.Sku }).IsUnique().HasDatabaseName("ix_products_tenant_sku");
        builder.HasIndex(p => new { p.TenantId, p.Barcode }).IsUnique().HasFilter("barcode IS NOT NULL").HasDatabaseName("ix_products_tenant_barcode");
        builder.HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
