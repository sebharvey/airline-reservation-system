using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Customer.Domain.Entities;

namespace ReservationSystem.Microservices.Customer.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Customer and LoyaltyTransaction tables.
/// Maps to the [customer] schema in SQL Server.
/// </summary>
public sealed class CustomerDbContext : DbContext
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers", "customer");

            entity.HasKey(c => c.CustomerId);

            entity.Property(c => c.CustomerId)
                  .HasColumnName("CustomerId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(c => c.LoyaltyNumber)
                  .HasColumnName("LoyaltyNumber")
                  .HasColumnType("nvarchar(20)")
                  .IsRequired();

            entity.HasIndex(c => c.LoyaltyNumber)
                  .IsUnique();

            entity.Property(c => c.IdentityReference)
                  .HasColumnName("IdentityReference")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired(false);

            entity.Property(c => c.GivenName)
                  .HasColumnName("GivenName")
                  .HasColumnType("nvarchar(100)")
                  .IsRequired();

            entity.Property(c => c.Surname)
                  .HasColumnName("Surname")
                  .HasColumnType("nvarchar(100)")
                  .IsRequired();

            entity.Property(c => c.DateOfBirth)
                  .HasColumnName("DateOfBirth")
                  .HasColumnType("date")
                  .IsRequired(false);

            entity.Property(c => c.Nationality)
                  .HasColumnName("Nationality")
                  .HasColumnType("nvarchar(3)")
                  .IsRequired(false);

            entity.Property(c => c.PreferredLanguage)
                  .HasColumnName("PreferredLanguage")
                  .HasColumnType("nvarchar(10)")
                  .IsRequired();

            entity.Property(c => c.PhoneNumber)
                  .HasColumnName("PhoneNumber")
                  .HasColumnType("nvarchar(30)")
                  .IsRequired(false);

            entity.Property(c => c.TierCode)
                  .HasColumnName("TierCode")
                  .HasColumnType("nvarchar(10)")
                  .IsRequired();

            entity.Property(c => c.PointsBalance)
                  .HasColumnName("PointsBalance")
                  .HasColumnType("int");

            entity.Property(c => c.TierProgressPoints)
                  .HasColumnName("TierProgressPoints")
                  .HasColumnType("int");

            entity.Property(c => c.IsActive)
                  .HasColumnName("IsActive")
                  .HasColumnType("bit");

            entity.Property(c => c.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset");

            entity.Property(c => c.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset");
        });

        modelBuilder.Entity<LoyaltyTransaction>(entity =>
        {
            entity.ToTable("LoyaltyTransactions", "customer");

            entity.HasKey(t => t.TransactionId);

            entity.Property(t => t.TransactionId)
                  .HasColumnName("TransactionId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(t => t.LoyaltyNumber)
                  .HasColumnName("LoyaltyNumber")
                  .HasColumnType("nvarchar(20)")
                  .IsRequired();

            entity.Property(t => t.TransactionType)
                  .HasColumnName("TransactionType")
                  .HasColumnType("nvarchar(20)")
                  .IsRequired();

            entity.Property(t => t.PointsDelta)
                  .HasColumnName("PointsDelta")
                  .HasColumnType("int");

            entity.Property(t => t.BalanceAfter)
                  .HasColumnName("BalanceAfter")
                  .HasColumnType("int");

            entity.Property(t => t.BookingReference)
                  .HasColumnName("BookingReference")
                  .HasColumnType("nvarchar(10)")
                  .IsRequired(false);

            entity.Property(t => t.FlightNumber)
                  .HasColumnName("FlightNumber")
                  .HasColumnType("nvarchar(10)")
                  .IsRequired(false);

            entity.Property(t => t.Description)
                  .HasColumnName("Description")
                  .HasColumnType("nvarchar(500)")
                  .IsRequired();

            entity.Property(t => t.TransactionDate)
                  .HasColumnName("TransactionDate")
                  .HasColumnType("datetimeoffset");

            entity.Property(t => t.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset");

            entity.Property(t => t.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset");
        });
    }
}
