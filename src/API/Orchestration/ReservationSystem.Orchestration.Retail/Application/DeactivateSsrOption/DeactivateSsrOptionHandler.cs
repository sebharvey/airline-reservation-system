using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.DeactivateSsrOption;

public sealed class DeactivateSsrOptionHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public DeactivateSsrOptionHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<bool> HandleAsync(DeactivateSsrOptionCommand command, CancellationToken cancellationToken)
    {
        return await _orderServiceClient.DeactivateSsrOptionAsync(command.SsrCode, cancellationToken);
    }
}
