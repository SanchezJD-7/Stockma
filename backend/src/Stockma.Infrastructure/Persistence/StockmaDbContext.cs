using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Stockma.Application.Common;
using Stockma.Domain.Common;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Identity;
using Stockma.Infrastructure.Persistence.Interceptors;

namespace Stockma.Infrastructure.Persistence;

public class StockmaDbContext(
    DbContextOptions<StockmaDbContext> options,
    ITenantContext tenantContext) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();
    public DbSet<DeviceOtp> DeviceOtps => Set<DeviceOtp>();
    public Guid CurrentTenantId => tenantContext.TenantId;
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new TenantImmutabilityInterceptor());
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockmaDbContext).Assembly);
        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var entity = Expression.Parameter(entityType.ClrType, "e");

            var tenantProperty = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                [typeof(Guid)],
                entity,
                Expression.Constant(nameof(ITenantEntity.TenantId)));

            var currentTenant = Expression.Property(
                Expression.Constant(this, typeof(StockmaDbContext)),
                nameof(CurrentTenantId));

            var comparison = Expression.Equal(tenantProperty, currentTenant);

            modelBuilder
                .Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(comparison, entity));
        }
    }
}
