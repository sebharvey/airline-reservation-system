using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Microservices.Identity.Models.Responses;

namespace ReservationSystem.Microservices.Identity.Application.CreateAccount;

/// <summary>
/// Handles the <see cref="CreateAccountCommand"/>.
/// Creates and persists a new user account.
/// </summary>
public sealed class CreateAccountHandler
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ILogger<CreateAccountHandler> _logger;

    public CreateAccountHandler(
        IUserAccountRepository userAccountRepository,
        ILogger<CreateAccountHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _logger = logger;
    }

    public Task<CreateAccountResponse> HandleAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
