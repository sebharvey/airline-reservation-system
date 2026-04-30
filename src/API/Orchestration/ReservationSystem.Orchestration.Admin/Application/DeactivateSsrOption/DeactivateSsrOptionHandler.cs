using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Admin.Application.DeactivateSsrOption;

public sealed class DeactivateSsrOptionHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public DeactivateSsrOptionHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<bool> HandleAsync(DeactivateSsrOptionCommand command, CancellationToken cancellationToken)
    {
        return await _orderServiceClient.DeactivateSsrOptionAsync(command.SsrCode.ToUpperInvariant(), cancellationToken);
    }
}
