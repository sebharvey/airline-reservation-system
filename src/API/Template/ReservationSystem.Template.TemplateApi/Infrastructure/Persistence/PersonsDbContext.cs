using Microsoft.EntityFrameworkCore;
using ReservationSystem.Template.TemplateApi.Domain.Entities;

namespace ReservationSystem.Template.TemplateApi.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the [dbo].[Persons] table.
///
/// Configures the Person entity mapping to match the existing SQL Server schema exactly —
/// column names, types, nullability, and primary key are all aligned with the DDL:
///
///   CREATE TABLE [dbo].[Persons](
///       [PersonID]   INT          NOT NULL PRIMARY KEY,
///       [LastName]   VARCHAR(255) NOT NULL,
///       [FirstName]  VARCHAR(255) NULL,
///       [Address]    VARCHAR(255) NULL,
///       [City]       VARCHAR(255) NULL
///   )
///
/// EF Core's HasNoKey / HasKey and HasColumnType ensure no EF migrations are needed
/// to alter the existing schema.
/// </summary>
public sealed class PersonsDbContext : DbContext
{
    public PersonsDbContext(DbContextOptions<PersonsDbContext> options) : base(options) { }

    public DbSet<Person> Persons => Set<Person>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("Persons", "dbo");

            entity.HasKey(p => p.PersonID);

            entity.Property(p => p.PersonID)
                  .HasColumnName("PersonID")
                  .HasColumnType("int")
                  .ValueGeneratedNever(); // No IDENTITY — application supplies the PK

            entity.Property(p => p.LastName)
                  .HasColumnName("LastName")
                  .HasColumnType("varchar(255)")
                  .IsRequired();

            entity.Property(p => p.FirstName)
                  .HasColumnName("FirstName")
                  .HasColumnType("varchar(255)")
                  .IsRequired(false);

            entity.Property(p => p.Address)
                  .HasColumnName("Address")
                  .HasColumnType("varchar(255)")
                  .IsRequired(false);

            entity.Property(p => p.City)
                  .HasColumnName("City")
                  .HasColumnType("varchar(255)")
                  .IsRequired(false);
        });
    }
}
