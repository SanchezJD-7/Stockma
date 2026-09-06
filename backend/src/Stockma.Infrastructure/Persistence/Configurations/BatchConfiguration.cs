using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockma.Domain.Entities;

namespace Stockma.Infrastructure.Persistence.Configurations;

public sealed class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("batches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.TenantId).HasColumnName("tenant_id");
        builder.Property(b => b.ProductId).HasColumnName("product_id");
        builder.Property(b => b.LotNumber).HasColumnName("lot_number").HasMaxLength(64).IsRequired();
        builder.Property(b => b.ExpirationDate).HasColumnName("expiration_date");
        builder.Property(b => b.CurrentQuantity).HasColumnName("current_quantity");
        builder.Property(b => b.LocationShelf).HasColumnName("location_shelf").HasMaxLength(64).IsRequired();
        builder.Property(b => b.Status).HasColumnName("status").HasConversion<int>();
        builder.Property<uint>("Xmin").HasColumnName("xmin").IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(b => new { b.ProductId, b.ExpirationDate }).HasDatabaseName("ix_batches_product_expiration");
        builder.HasOne<Product>().WithMany().HasForeignKey(b => b.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(b => b.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
