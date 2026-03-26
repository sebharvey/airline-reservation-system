using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Payment.Domain.Entities;

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

    public DbSet<Domain.Entities.Payment> Payments => Set<Domain.Entities.Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Entities.Payment>(entity =>
        {
            entity.ToTable("Payment", "payment", t =>
            {
                t.HasTrigger("TR_Payment_UpdatedAt");
                t.UseSqlOutputClause(false);
            });

            entity.HasKey(p => p.PaymentId);

            entity.Property(p => p.PaymentId)
                  .HasColumnName("PaymentId")
                  .HasColumnType("uniqueidentifier")
                  .ValueGeneratedNever();

            entity.Property(p => p.BookingReference)
                  .HasColumnName("BookingReference")
                  .HasColumnType("char(6)")
                  .IsRequired(false);

            entity.Property(p => p.PaymentType)
                  .HasColumnName("PaymentType")
                  .HasColumnType("varchar(30)")
                  .IsRequired();

            entity.Property(p => p.Method)
                  .HasColumnName("Method")
                  .HasColumnType("varchar(20)")
                  .IsRequired();

            entity.Property(p => p.CardType)
                  .HasColumnName("CardType")
                  .HasColumnType("varchar(20)")
                  .IsRequired(false);

            entity.Property(p => p.CardLast4)
                  .HasColumnName("CardLast4")
                  .HasColumnType("char(4)")
                  .IsRequired(false);

            entity.Property(p => p.CurrencyCode)
                  .HasColumnName("CurrencyCode")
                  .HasColumnType("char(3)")
                  .IsRequired();

            entity.Property(p => p.Amount)
                  .HasColumnName("Amount")
                  .HasColumnType("decimal(10,2)")
                  .IsRequired();

            entity.Property(p => p.AuthorisedAmount)
                  .HasColumnName("AuthorisedAmount")
                  .HasColumnType("decimal(10,2)")
                  .IsRequired(false);

            entity.Property(p => p.SettledAmount)
                  .HasColumnName("SettledAmount")
                  .HasColumnType("decimal(10,2)")
                  .IsRequired(false);

            entity.Property(p => p.Status)
                  .HasColumnName("Status")
                  .HasColumnType("varchar(20)")
                  .IsRequired();

            entity.Property(p => p.AuthorisedAt)
                  .HasColumnName("AuthorisedAt")
                  .HasColumnType("datetime2")
                  .IsRequired(false);

            entity.Property(p => p.SettledAt)
                  .HasColumnName("SettledAt")
                  .HasColumnType("datetime2")
                  .IsRequired(false);

            entity.Property(p => p.Description)
                  .HasColumnName("Description")
                  .HasColumnType("varchar(255)")
                  .IsRequired(false);

            entity.Property(p => p.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.Property(p => p.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();
        });

        modelBuilder.Entity<PaymentEvent>(entity =>
        {
            entity.ToTable("PaymentEvent", "payment", t =>
            {
                t.HasTrigger("TR_PaymentEvent_UpdatedAt");
                t.UseSqlOutputClause(false);
            });

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
                  .HasColumnType("varchar(20)")
                  .IsRequired();

            entity.Property(pe => pe.Amount)
                  .HasColumnName("Amount")
                  .HasColumnType("decimal(10,2)")
                  .IsRequired();

            entity.Property(pe => pe.CurrencyCode)
                  .HasColumnName("CurrencyCode")
                  .HasColumnType("char(3)")
                  .IsRequired();

            entity.Property(pe => pe.Notes)
                  .HasColumnName("Notes")
                  .HasColumnType("varchar(255)")
                  .IsRequired(false);

            entity.Property(pe => pe.CreatedAt)
                  .HasColumnName("CreatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.Property(pe => pe.UpdatedAt)
                  .HasColumnName("UpdatedAt")
                  .HasColumnType("datetime2")
                  .IsRequired();

            entity.HasOne<Domain.Entities.Payment>()
                  .WithMany()
                  .HasForeignKey(pe => pe.PaymentId);
        });
    }
}
