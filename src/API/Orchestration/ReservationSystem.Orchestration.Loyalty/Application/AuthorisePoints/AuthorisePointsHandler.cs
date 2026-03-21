using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Loyalty.Application.AuthorisePoints;

public sealed class AuthorisePointsHandler
{
    private readonly CustomerServiceClient _customerServiceClient;

    public AuthorisePointsHandler(CustomerServiceClient customerServiceClient)
    {
        _customerServiceClient = customerServiceClient;
    }

    public Task<object> HandleAsync(AuthorisePointsCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
