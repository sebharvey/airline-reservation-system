using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;

namespace ReservationSystem.Template.TemplateApi.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IPersonRepository"/>.
///
/// Uses <see cref="PersonsDbContext"/> to interact with the [dbo].[Persons] table.
/// The DbContext is scoped (one per function invocation) so no manual connection
/// management is required — EF handles connection lifetime internally.
///
/// Data flow (read):
///   EF query → tracked <see cref="Person"/> entity
///
/// Data flow (write):
///   <see cref="Person"/> domain entity → EF change tracker → SQL INSERT / UPDATE / DELETE
/// </summary>
public sealed class EfPersonRepository : IPersonRepository
{
    private readonly PersonsDbContext _dbContext;
    private readonly ILogger<EfPersonRepository> _logger;

    public EfPersonRepository(PersonsDbContext dbContext, ILogger<EfPersonRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Person?> GetByIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonID == personId, cancellationToken);
    }

    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var persons = await _dbContext.Persons
            .AsNoTracking()
            .OrderBy(p => p.PersonID)
            .ToListAsync(cancellationToken);

        return persons.AsReadOnly();
    }

    public async Task CreateAsync(Person person, CancellationToken cancellationToken = default)
    {
        _dbContext.Persons.Add(person);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted Person {PersonID} into [dbo].[Persons]", person.PersonID);
    }

    public async Task UpdateAsync(Person person, CancellationToken cancellationToken = default)
    {
        _dbContext.Persons.Update(person);
        var rowsAffected = await _dbContext.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Person {PersonID}", person.PersonID);
        else
            _logger.LogDebug("Updated Person {PersonID} in [dbo].[Persons]", person.PersonID);
    }

    public async Task<bool> DeleteAsync(int personId, CancellationToken cancellationToken = default)
    {
        var person = await _dbContext.Persons
            .FirstOrDefaultAsync(p => p.PersonID == personId, cancellationToken);

        if (person is null)
            return false;

        _dbContext.Persons.Remove(person);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted Person {PersonID} from [dbo].[Persons]", personId);
        return true;
    }

    public async Task<bool> ExistsAsync(int personId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Persons
            .AsNoTracking()
            .AnyAsync(p => p.PersonID == personId, cancellationToken);
    }
}
