using ReservationSystem.Microservices.Identity.Domain.Entities;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.GetAccount;

/// <summary>
/// Handles the <see cref="GetAccountQuery"/>.
/// Retrieves a user account by its ID, returning <c>null</c> if not found.
/// </summary>
public sealed class GetAccountHandler
{
    private readonly IUserAccountRepository _repository;

    public GetAccountHandler(IUserAccountRepository repository)
    {
        _repository = repository;
    }

    public Task<UserAccount?> HandleAsync(GetAccountQuery query, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(query.UserAccountId, cancellationToken);
}
