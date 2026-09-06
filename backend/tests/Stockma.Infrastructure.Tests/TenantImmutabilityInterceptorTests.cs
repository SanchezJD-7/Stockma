using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Domain.Common;
using Stockma.Infrastructure.Persistence.Interceptors;

namespace Stockma.Infrastructure.Tests;

public class TenantScopedEntity : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class InterceptorTestContext(DbContextOptions<InterceptorTestContext> options) : DbContext(options)
{
    public DbSet<TenantScopedEntity> Entities => Set<TenantScopedEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new TenantImmutabilityInterceptor());
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantScopedEntity>().ToTable("tenant_scoped_entities");
        base.OnModelCreating(modelBuilder);
    }
}

public class TenantImmutabilityInterceptorTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private InterceptorTestContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InterceptorTestContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        return new InterceptorTestContext(options);
    }

    [Fact]
    public async Task ChangingTenantId_IsRejectedAndOriginalPersists()
    {
        var id = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Entities.Add(new TenantScopedEntity { Id = id, TenantId = TenantA, Name = "original" });
            await seed.SaveChangesAsync();
        }

        await using (var update = CreateContext())
        {
            var entity = await update.Entities.SingleAsync(e => e.Id == id);
            entity.TenantId = TenantB;

            var act = async () => await update.SaveChangesAsync();

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*inmutable*FR-004*");
        }

        await using var verification = CreateContext();
        var persisted = await verification.Entities.SingleAsync(e => e.Id == id);
        persisted.TenantId.Should().Be(TenantA, "el TenantId original debe persistir intacto");
    }

    [Fact]
    public async Task ChangingOtherProperties_IsNotBlocked()
    {
        var id = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Entities.Add(new TenantScopedEntity { Id = id, TenantId = TenantA, Name = "before" });
            await seed.SaveChangesAsync();
        }

        await using (var update = CreateContext())
        {
            var entity = await update.Entities.SingleAsync(e => e.Id == id);
            entity.Name = "after";
            await update.SaveChangesAsync();
        }

        await using var verification = CreateContext();
        var persisted = await verification.Entities.SingleAsync(e => e.Id == id);
        persisted.Name.Should().Be("after", "el interceptor sólo debe bloquear cambios de TenantId");
    }
}
