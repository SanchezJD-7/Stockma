using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockma.Domain.Entities;
using Stockma.Domain.Waitlist;

namespace Stockma.Infrastructure.Persistence.Configurations;

public sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public const string EmailIndexName = "IX_waitlist_entries_email";

    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("waitlist_entries");

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(WaitlistRules.MaxNameLength).IsRequired();
        builder.Property(e => e.CountryCode).HasColumnName("country_code").HasMaxLength(WaitlistRules.MaxCountryCodeLength).IsRequired();
        builder.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(WaitlistRules.MaxPhoneDigits).IsRequired();
        builder.Property(e => e.Email).HasColumnName("email").HasMaxLength(WaitlistRules.MaxEmailLength).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(e => e.Email).IsUnique().HasDatabaseName(EmailIndexName);
    }
}
