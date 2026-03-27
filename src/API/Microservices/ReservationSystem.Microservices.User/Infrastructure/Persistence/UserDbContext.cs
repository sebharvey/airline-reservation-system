using Microsoft.EntityFrameworkCore;

namespace ReservationSystem.Microservices.User.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the user schema.
///
/// Configures entity mappings to match the SQL Server schema:
///   user.User  - employee user accounts with credentials
/// </summary>
public sealed class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    public DbSet<Domain.Entities.User> Users => Set<Domain.Entities.User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Entities.User>(entity =>
        {
            entity.ToTable("User", "user", t =>
            {
                t.HasTrigger("TR_User_UpdatedAt");
                t.UseSqlOutputClause(false);
            });

            entity.HasKey(u => u.UserId);

            entity.Property(u => u.UserId)
                  .HasColumnName("UserId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(u => u.Username)
                  .HasColumnName("Username")
                  .HasColumnType("varchar(100)")
                  .IsRequired();

            entity.Property(u => u.Email)
                  .HasColumnName("Email")
                  .HasColumnType("varchar(254)")
                  .IsRequired();

            entity.Property(u => u.PasswordHash)
                  .HasColumnName("PasswordHash")
                  .HasColumnType("varchar(255)")
                  .IsRequired();

            entity.Property(u => u.FirstName)
                  .HasColumnName("FirstName")
                  .HasColumnType("nvarchar(100)")
                  .IsRequired();

            entity.Property(u => u.LastName)
                  .HasColumnName("LastName")
                  .HasColumnType("nvarchar(100)")
                  .IsRequired();

            entity.Property(u => u.IsActive)
                  .HasColumnName("IsActive")
                  .HasColumnType("bit")
                  .IsRequired();

            entity.Property(u => u.IsLocked)
                  .HasColumnName("IsLocked")
                  .HasColumnType("bit")
                  .IsRequired();

            entity.Property(u => u.FailedLoginAttempts)
                  .HasColumnName("FailedLoginAttempts")
                  .HasColumnType("tinyint")
                  .IsRequired();

            entity.Property(u => u.LastLoginAt)
                  .HasColumnName("LastLoginAt")
                  .HasColumnType("datetime2")
                  .IsRequired(false);

            entity.Property(u => u.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.Property(u => u.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });
    }
}
