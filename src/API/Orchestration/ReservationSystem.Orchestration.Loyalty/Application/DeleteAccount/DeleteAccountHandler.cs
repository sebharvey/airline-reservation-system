using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.DeleteAccount;

public sealed class DeleteAccountHandler
{
    private readonly CustomerServiceClient _customerServiceClient;

    public DeleteAccountHandler(CustomerServiceClient customerServiceClient)
    {
        _customerServiceClient = customerServiceClient;
    }

    /// <returns>True if deleted; false if the customer was not found.</returns>
    public async Task<bool> HandleAsync(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        return await _customerServiceClient.DeleteCustomerAsync(command.LoyaltyNumber, cancellationToken);
    }
}
