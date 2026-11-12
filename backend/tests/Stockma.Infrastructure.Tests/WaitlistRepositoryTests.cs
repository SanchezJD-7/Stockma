using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Application.Waitlist.Exceptions;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;
using Stockma.Infrastructure.Waitlist;

namespace Stockma.Infrastructure.Tests;

public class WaitlistRepositoryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private StockmaDbContext CreateContext()
    {
        var tenantContext = new TenantContext();

        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        return new StockmaDbContext(options, tenantContext);
    }

    [Fact]
    public async Task AddAsync_PersistsEntry_AndReadsItBack()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var repository = new WaitlistRepository(context);
        var entry = new WaitlistEntry("Persona Prueba", "+57", "300 123 4567", "Prueba@Ejemplo.com");

        await repository.AddAsync(entry);

        var saved = await context.WaitlistEntries
            .AsNoTracking()
            .SingleAsync(e => e.Id == entry.Id);

        saved.Email.Should().Be("prueba@ejemplo.com", "el correo se guarda normalizado a minúsculas");
        saved.FullName.Should().Be("Persona Prueba");
        saved.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AddAsync_WithDuplicateEmailInDifferentCasing_ThrowsWaitlistDuplicate()
    {
        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();

            var repository = new WaitlistRepository(context);
            await repository.AddAsync(
                new WaitlistEntry("Primera Persona", "+57", "300 123 4567", "dup@ejemplo.com"));
        }
        
        await using var duplicateContext = CreateContext();
        var duplicateRepository = new WaitlistRepository(duplicateContext);

        var act = async () => await duplicateRepository.AddAsync(
            new WaitlistEntry("Segunda Persona", "+57", "300 987 6543", "DUP@Ejemplo.com"));

        var exception = (await act.Should().ThrowAsync<WaitlistEmailAlreadyRegisteredException>())
            .Which;

        exception.ErrorCode.Should().Be("WAITLIST_EMAIL_DUPLICATE");
        exception.Message.Should().Contain("dup@ejemplo.com", "el detalle usa el correo normalizado");
    }
}
