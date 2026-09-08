using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Identity;

namespace Stockma.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");
        builder.Property(u => u.UserName).HasColumnName("user_name").HasMaxLength(256);
        builder.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(256);
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(u => u.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(256);
        builder.Property(u => u.EmailConfirmed).HasColumnName("email_confirmed");
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");
        builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp").IsConcurrencyToken();
        builder.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        builder.Property(u => u.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        builder.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count");
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("ux_users_normalized_email");
        builder.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("ux_users_normalized_user_name");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("roles");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(64);
        builder.Property(r => r.NormalizedName).HasColumnName("normalized_name").HasMaxLength(64);
        builder.Property(r => r.ConcurrencyStamp).HasColumnName("concurrency_stamp").IsConcurrencyToken();

        builder.HasIndex(r => r.NormalizedName).IsUnique().HasDatabaseName("ux_roles_normalized_name");
    }
}

public sealed class IdentityUserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        builder.ToTable("user_roles");
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.RoleId).HasColumnName("role_id");
    }
}

public sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("user_claims");
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.ClaimType).HasColumnName("claim_type");
        builder.Property(c => c.ClaimValue).HasColumnName("claim_value");
    }
}

public sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("user_logins");
        builder.Property(l => l.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
        builder.Property(l => l.ProviderKey).HasColumnName("provider_key").HasMaxLength(128);
        builder.Property(l => l.ProviderDisplayName).HasColumnName("provider_display_name");
        builder.Property(l => l.UserId).HasColumnName("user_id");
    }
}

public sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_tokens");
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(t => t.Value).HasColumnName("value");
    }
}

public sealed class IdentityRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        builder.ToTable("role_claims");
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.RoleId).HasColumnName("role_id");
        builder.Property(c => c.ClaimType).HasColumnName("claim_type");
        builder.Property(c => c.ClaimValue).HasColumnName("claim_value");
    }
}
