using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Application.Login;
using ReservationSystem.Microservices.Identity.Domain.Entities;
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

    public async Task<CreateAccountResponse> HandleAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        var existing = await _userAccountRepository.GetByEmailAsync(command.Email, cancellationToken);

        if (existing is not null)
        {
            _logger.LogDebug("Account creation failed: email already registered");
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var passwordHash = LoginHandler.HashPassword(command.Password);
        var account = UserAccount.Create(command.Email, passwordHash);

        await _userAccountRepository.CreateAsync(account, cancellationToken);

        _logger.LogInformation("Created user account {UserAccountId}", account.UserAccountId);

        return new CreateAccountResponse
        {
            UserAccountId = account.UserAccountId,
            IdentityReference = account.IdentityReference
        };
    }
}
