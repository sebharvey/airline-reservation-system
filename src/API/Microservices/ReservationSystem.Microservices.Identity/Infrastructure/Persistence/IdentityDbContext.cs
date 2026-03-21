using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Identity.Domain.Entities;

namespace ReservationSystem.Microservices.Identity.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the identity schema.
///
/// Configures entity mappings to match the SQL Server schema:
///   identity.UserAccount  - user accounts with credentials
///   identity.RefreshToken - issued refresh tokens
/// </summary>
public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccount", "identity");

            entity.HasKey(u => u.UserAccountId);

            entity.Property(u => u.UserAccountId)
                  .HasColumnName("UserAccountId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.HasIndex(u => u.IdentityReference)
                  .IsUnique();

            entity.Property(u => u.IdentityReference)
                  .HasColumnName("IdentityReference")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(u => u.Email)
                  .HasColumnName("Email")
                  .HasColumnType("nvarchar(320)")
                  .IsRequired();

            entity.Property(u => u.PasswordHash)
                  .HasColumnName("PasswordHash")
                  .HasColumnType("nvarchar(500)")
                  .IsRequired();

            entity.Property(u => u.IsEmailVerified)
                  .HasColumnName("IsEmailVerified")
                  .HasColumnType("bit")
                  .IsRequired();

            entity.Property(u => u.IsLocked)
                  .HasColumnName("IsLocked")
                  .HasColumnType("bit")
                  .IsRequired();

            entity.Property(u => u.FailedLoginAttempts)
                  .HasColumnName("FailedLoginAttempts")
                  .HasColumnType("int")
                  .IsRequired();

            entity.Property(u => u.LastLoginAt)
                  .HasColumnName("LastLoginAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired(false);

            entity.Property(u => u.PasswordChangedAt)
                  .HasColumnName("PasswordChangedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(u => u.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(u => u.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshToken", "identity");

            entity.HasKey(r => r.RefreshTokenId);

            entity.Property(r => r.RefreshTokenId)
                  .HasColumnName("RefreshTokenId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(r => r.UserAccountId)
                  .HasColumnName("UserAccountId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(r => r.TokenHash)
                  .HasColumnName("TokenHash")
                  .HasColumnType("nvarchar(500)")
                  .IsRequired();

            entity.Property(r => r.DeviceHint)
                  .HasColumnName("DeviceHint")
                  .HasColumnType("nvarchar(200)")
                  .IsRequired(false);

            entity.Property(r => r.IsRevoked)
                  .HasColumnName("IsRevoked")
                  .HasColumnType("bit")
                  .IsRequired();

            entity.Property(r => r.ExpiresAt)
                  .HasColumnName("ExpiresAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(r => r.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(r => r.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.HasOne<UserAccount>()
                  .WithMany()
                  .HasForeignKey(r => r.UserAccountId);
        });
    }
}
