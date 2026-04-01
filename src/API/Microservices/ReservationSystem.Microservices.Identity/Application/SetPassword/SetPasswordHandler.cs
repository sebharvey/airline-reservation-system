using ReservationSystem.Microservices.Identity.Domain.Repositories;

namespace ReservationSystem.Microservices.Identity.Application.SetPassword;

/// <summary>
/// Handles <see cref="SetPasswordCommand"/>.
/// Directly sets a new password on a user account without requiring a reset token.
/// Intended for staff-initiated password assignment (e.g. terminal admin operations).
/// </summary>
public sealed class SetPasswordHandler
{
    private readonly IUserAccountRepository _repository;

    public SetPasswordHandler(IUserAccountRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(SetPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetByIdAsync(command.UserAccountId, cancellationToken)
            ?? throw new KeyNotFoundException($"No user account found for ID '{command.UserAccountId}'.");

        account.ChangePassword(command.NewPasswordHash);

        await _repository.UpdateAsync(account, cancellationToken);
    }
}
