using ReservationSystem.Microservices.Identity.Domain.Entities;
using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.GetAccountByEmail;

/// <summary>
/// Handles the <see cref="GetAccountByEmailQuery"/>.
/// Retrieves a user account by its email address, returning <c>null</c> if not found.
/// </summary>
public sealed class GetAccountByEmailHandler
{
    private readonly IUserAccountRepository _repository;

    public GetAccountByEmailHandler(IUserAccountRepository repository)
    {
        _repository = repository;
    }

    public Task<UserAccount?> HandleAsync(GetAccountByEmailQuery query, CancellationToken cancellationToken = default)
        => _repository.GetByEmailAsync(query.Email, cancellationToken);
}
