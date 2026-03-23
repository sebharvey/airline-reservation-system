using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Loyalty.Application.GetTransactions;

public sealed class GetTransactionsHandler
{
    private readonly CustomerServiceClient _customerServiceClient;

    public GetTransactionsHandler(CustomerServiceClient customerServiceClient)
    {
        _customerServiceClient = customerServiceClient;
    }

    public Task<TransactionsDto?> HandleAsync(GetTransactionsQuery query, CancellationToken cancellationToken)
    {
        return _customerServiceClient.GetTransactionsAsync(
            query.LoyaltyNumber, query.Page, query.PageSize, cancellationToken);
    }
}
