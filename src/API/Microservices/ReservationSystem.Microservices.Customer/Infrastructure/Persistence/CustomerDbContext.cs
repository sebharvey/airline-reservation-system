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

    public DbSet<Domain.Entities.Customer> Customers => Set<Domain.Entities.Customer>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Entities.Customer>(entity =>
        {
            entity.ToTable("Customer", "customer", t =>
            {
                t.HasTrigger("TR_Customer_UpdatedAt");
                t.UseSqlOutputClause(false);
            });

            entity.HasKey(c => c.CustomerId);

            entity.Property(c => c.CustomerId)
                  .HasColumnName("CustomerId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(c => c.LoyaltyNumber)
                  .HasColumnName("LoyaltyNumber")
                  .HasColumnType("varchar(20)")
                  .IsRequired();

            entity.HasIndex(c => c.LoyaltyNumber)
                  .IsUnique();

            entity.Property(c => c.IdentityId)
                  .HasColumnName("IdentityId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired(false);

            entity.Property(c => c.GivenName)
                  .HasColumnName("GivenName")
                  .HasColumnType("varchar(100)")
                  .IsRequired();

            entity.Property(c => c.Surname)
                  .HasColumnName("Surname")
                  .HasColumnType("varchar(100)")
                  .IsRequired();

            entity.Property(c => c.DateOfBirth)
                  .HasColumnName("DateOfBirth")
                  .HasColumnType("date")
                  .IsRequired(false);

            entity.Property(c => c.Nationality)
                  .HasColumnName("Nationality")
                  .HasColumnType("char(3)")
                  .IsRequired(false)
                  .HasConversion(v => v, v => v != null ? v.TrimEnd() : null);

            entity.Property(c => c.PreferredLanguage)
                  .HasColumnName("PreferredLanguage")
                  .HasColumnType("char(5)")
                  .IsRequired()
                  .HasConversion(v => v, v => v != null ? v.TrimEnd() : v);

            entity.Property(c => c.PhoneNumber)
                  .HasColumnName("PhoneNumber")
                  .HasColumnType("varchar(30)")
                  .IsRequired(false);

            entity.Property(c => c.TierCode)
                  .HasColumnName("TierCode")
                  .HasColumnType("varchar(20)")
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
                  .HasColumnType("datetime2");

            entity.Property(c => c.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2");
        });

        modelBuilder.Entity<LoyaltyTransaction>(entity =>
        {
            entity.ToTable("LoyaltyTransaction", "customer", t =>
            {
                t.HasTrigger("TR_LoyaltyTransaction_UpdatedAt");
                t.UseSqlOutputClause(false);
            });

            entity.HasKey(t => t.TransactionId);

            entity.Property(t => t.TransactionId)
                  .HasColumnName("TransactionId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(t => t.CustomerId)
                  .HasColumnName("CustomerId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(t => t.TransactionType)
                  .HasColumnName("TransactionType")
                  .HasColumnType("varchar(20)")
                  .IsRequired();

            entity.Property(t => t.PointsDelta)
                  .HasColumnName("PointsDelta")
                  .HasColumnType("int");

            entity.Property(t => t.BalanceAfter)
                  .HasColumnName("BalanceAfter")
                  .HasColumnType("int");

            entity.Property(t => t.BookingReference)
                  .HasColumnName("BookingReference")
                  .HasColumnType("char(6)")
                  .IsRequired(false);

            entity.Property(t => t.FlightNumber)
                  .HasColumnName("FlightNumber")
                  .HasColumnType("varchar(10)")
                  .IsRequired(false);

            entity.Property(t => t.Description)
                  .HasColumnName("Description")
                  .HasColumnType("varchar(255)")
                  .IsRequired();

            entity.Property(t => t.TransactionDate)
                  .HasColumnName("TransactionDate")
                  .HasColumnType("datetime2");

            entity.Property(t => t.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2");

            entity.Property(t => t.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2");

            entity.HasOne<Domain.Entities.Customer>()
                  .WithMany()
                  .HasForeignKey(t => t.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
