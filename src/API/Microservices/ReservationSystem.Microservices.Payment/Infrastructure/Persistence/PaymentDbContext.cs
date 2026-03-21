using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Payment.Domain.Entities;
using Payment = ReservationSystem.Microservices.Payment.Domain.Entities.Payment;

namespace ReservationSystem.Microservices.Payment.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the payment schema.
///
/// Configures entity mappings to match the SQL Server schema:
///   payment.Payment  - payment transactions
///   payment.PaymentEvent - payment lifecycle audit events
/// </summary>
public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payment", "payment");

            entity.HasKey(p => p.PaymentId);

            entity.Property(p => p.PaymentId)
                  .HasColumnName("PaymentId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(p => p.PaymentReference)
                  .HasColumnName("PaymentReference")
                  .HasColumnType("nvarchar(50)")
                  .IsRequired();

            entity.Property(p => p.BookingReference)
                  .HasColumnName("BookingReference")
                  .HasColumnType("nvarchar(50)")
                  .IsRequired(false);

            entity.Property(p => p.PaymentType)
                  .HasColumnName("PaymentType")
                  .HasColumnType("nvarchar(50)")
                  .IsRequired();

            entity.Property(p => p.Method)
                  .HasColumnName("Method")
                  .HasColumnType("nvarchar(50)")
                  .IsRequired();

            entity.Property(p => p.CardType)
                  .HasColumnName("CardType")
                  .HasColumnType("nvarchar(50)")
                  .IsRequired(false);

            entity.Property(p => p.CardLast4)
                  .HasColumnName("CardLast4")
                  .HasColumnType("nvarchar(4)")
                  .IsRequired(false);

            entity.Property(p => p.CurrencyCode)
                  .HasColumnName("CurrencyCode")
                  .HasColumnType("nvarchar(3)")
                  .IsRequired();

            entity.Property(p => p.AuthorisedAmount)
                  .HasColumnName("AuthorisedAmount")
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(p => p.SettledAmount)
                  .HasColumnName("SettledAmount")
                  .HasColumnType("decimal(18,2)")
                  .IsRequired(false);

            entity.Property(p => p.Status)
                  .HasColumnName("Status")
                  .HasColumnType("nvarchar(20)")
                  .IsRequired();

            entity.Property(p => p.AuthorisedAt)
                  .HasColumnName("AuthorisedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(p => p.SettledAt)
                  .HasColumnName("SettledAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired(false);

            entity.Property(p => p.Description)
                  .HasColumnName("Description")
                  .HasColumnType("nvarchar(500)")
                  .IsRequired(false);

            entity.Property(p => p.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(p => p.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();
        });

        modelBuilder.Entity<PaymentEvent>(entity =>
        {
            entity.ToTable("PaymentEvent", "payment");

            entity.HasKey(pe => pe.PaymentEventId);

            entity.Property(pe => pe.PaymentEventId)
                  .HasColumnName("PaymentEventId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(pe => pe.PaymentId)
                  .HasColumnName("PaymentId")
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.Property(pe => pe.EventType)
                  .HasColumnName("EventType")
                  .HasColumnType("nvarchar(50)")
                  .IsRequired();

            entity.Property(pe => pe.Amount)
                  .HasColumnName("Amount")
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(pe => pe.CurrencyCode)
                  .HasColumnName("CurrencyCode")
                  .HasColumnType("nvarchar(3)")
                  .IsRequired();

            entity.Property(pe => pe.Notes)
                  .HasColumnName("Notes")
                  .HasColumnType("nvarchar(500)")
                  .IsRequired(false);

            entity.Property(pe => pe.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.Property(pe => pe.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetimeoffset")
                  .IsRequired();

            entity.HasOne<Payment>()
                  .WithMany()
                  .HasForeignKey(pe => pe.PaymentId);
        });
    }
}
