using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.SearchCustomers;

/// <summary>
/// Handles the <see cref="SearchCustomersQuery"/>.
/// Delegates to the repository for a partial-match search across loyalty number,
/// given name and surname.
/// </summary>
public sealed class SearchCustomersHandler
{
    private readonly ICustomerRepository _repository;
    private readonly ILogger<SearchCustomersHandler> _logger;

    public SearchCustomersHandler(
        ICustomerRepository repository,
        ILogger<SearchCustomersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Domain.Entities.Customer>> HandleAsync(
        SearchCustomersQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = await _repository.SearchAsync(query.SearchTerm, cancellationToken);

        _logger.LogDebug("Search for '{SearchTerm}' returned {Count} result(s)", query.SearchTerm, results.Count);

        return results;
    }
}
