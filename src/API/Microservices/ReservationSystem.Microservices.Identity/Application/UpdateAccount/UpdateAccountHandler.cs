using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.UpdateAccount;

/// <summary>
/// Handles <see cref="UpdateAccountCommand"/>.
/// Applies admin-initiated changes to email and/or locked status without requiring
/// any verification step — the caller (Loyalty API staff endpoint) is responsible
/// for authorisation.
/// </summary>
public sealed class UpdateAccountHandler
{
    private readonly IUserAccountRepository _repository;

    public UpdateAccountHandler(IUserAccountRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(UpdateAccountCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetByIdAsync(command.UserAccountId, cancellationToken)
            ?? throw new KeyNotFoundException($"No user account found for ID '{command.UserAccountId}'.");

        if (command.Email is not null)
        {
            // Check uniqueness before applying.
            var existing = await _repository.GetByEmailAsync(command.Email, cancellationToken);
            if (existing is not null && existing.UserAccountId != command.UserAccountId)
                throw new InvalidOperationException("The email address is already registered to another account.");

            account.ChangeEmail(command.Email);
        }

        if (command.IsLocked.HasValue)
        {
            if (command.IsLocked.Value)
                account.Lock();
            else
                account.Unlock();
        }

        await _repository.UpdateAsync(account, cancellationToken);
    }
}
