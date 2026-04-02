using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using CustomerOrder = ReservationSystem.Microservices.Customer.Domain.Entities.CustomerOrder;

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
    public DbSet<CustomerPreferences> CustomerPreferences => Set<CustomerPreferences>();
    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();

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

            entity.Property(c => c.Gender)
                  .HasColumnName("Gender")
                  .HasColumnType("varchar(20)")
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

            entity.Property(c => c.AddressLine1)
                  .HasColumnName("AddressLine1")
                  .HasColumnType("varchar(150)")
                  .IsRequired(false);

            entity.Property(c => c.AddressLine2)
                  .HasColumnName("AddressLine2")
                  .HasColumnType("varchar(150)")
                  .IsRequired(false);

            entity.Property(c => c.City)
                  .HasColumnName("City")
                  .HasColumnType("varchar(100)")
                  .IsRequired(false);

            entity.Property(c => c.StateOrRegion)
                  .HasColumnName("StateOrRegion")
                  .HasColumnType("varchar(100)")
                  .IsRequired(false);

            entity.Property(c => c.PostalCode)
                  .HasColumnName("PostalCode")
                  .HasColumnType("varchar(20)")
                  .IsRequired(false);

            entity.Property(c => c.CountryCode)
                  .HasColumnName("CountryCode")
                  .HasColumnType("char(2)")
                  .IsRequired(false)
                  .HasConversion(v => v, v => v != null ? v.TrimEnd() : null);

            entity.Property(c => c.PassportNumber)
                  .HasColumnName("PassportNumber")
                  .HasColumnType("varchar(50)")
                  .IsRequired(false);

            entity.Property(c => c.PassportIssueDate)
                  .HasColumnName("PassportIssueDate")
                  .HasColumnType("date")
                  .IsRequired(false);

            entity.Property(c => c.PassportIssuer)
                  .HasColumnName("PassportIssuer")
                  .HasColumnType("char(2)")
                  .IsRequired(false)
                  .HasConversion(v => v, v => v != null ? v.TrimEnd() : null);

            entity.Property(c => c.PassportExpiryDate)
                  .HasColumnName("PassportExpiryDate")
                  .HasColumnType("date")
                  .IsRequired(false);

            entity.Property(c => c.KnownTravellerNumber)
                  .HasColumnName("KnownTravellerNumber")
                  .HasColumnType("varchar(50)")
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

        modelBuilder.Entity<CustomerPreferences>(entity =>
        {
            entity.ToTable("Preferences", "customer", t =>
            {
                t.HasTrigger("TR_Preferences_UpdatedAt");
                t.UseSqlOutputClause(false);
            });

            entity.HasKey(p => p.PreferenceId);

            entity.Property(p => p.PreferenceId)
                  .HasColumnName("PreferenceId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(p => p.CustomerId)
                  .HasColumnName("CustomerId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.HasIndex(p => p.CustomerId)
                  .IsUnique();

            entity.Property(p => p.MarketingEnabled)
                  .HasColumnName("MarketingEnabled")
                  .HasColumnType("bit");

            entity.Property(p => p.AnalyticsEnabled)
                  .HasColumnName("AnalyticsEnabled")
                  .HasColumnType("bit");

            entity.Property(p => p.FunctionalEnabled)
                  .HasColumnName("FunctionalEnabled")
                  .HasColumnType("bit");

            entity.Property(p => p.AppNotificationsEnabled)
                  .HasColumnName("AppNotificationsEnabled")
                  .HasColumnType("bit");

            entity.Property(p => p.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2");

            entity.Property(p => p.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2");

            entity.HasOne<Domain.Entities.Customer>()
                  .WithOne()
                  .HasForeignKey<CustomerPreferences>(p => p.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.ToTable("Order", "customer");

            entity.HasKey(o => o.CustomerOrderId);

            entity.Property(o => o.CustomerOrderId)
                  .HasColumnName("CustomerOrderId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(o => o.CustomerId)
                  .HasColumnName("CustomerId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(o => o.OrderId)
                  .HasColumnName("OrderId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.HasIndex(o => o.OrderId)
                  .IsUnique();

            entity.Property(o => o.BookingReference)
                  .HasColumnName("BookingReference")
                  .HasColumnType("char(6)")
                  .IsRequired()
                  .HasConversion(v => v, v => v != null ? v.TrimEnd() : v);

            entity.Property(o => o.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2");

            entity.HasOne<Domain.Entities.Customer>()
                  .WithMany()
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
