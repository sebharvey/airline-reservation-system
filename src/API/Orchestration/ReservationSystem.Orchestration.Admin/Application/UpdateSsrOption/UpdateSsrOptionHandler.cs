using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.UpdateSsrOption;

public sealed class UpdateSsrOptionHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public UpdateSsrOptionHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<SsrOptionResponse?> HandleAsync(UpdateSsrOptionCommand command, CancellationToken cancellationToken)
    {
        var body = new { label = command.Label, category = command.Category };
        var result = await _orderServiceClient.UpdateSsrOptionAsync(command.SsrCode.ToUpperInvariant(), body, cancellationToken);

        if (result is null)
            return null;

        return new SsrOptionResponse
        {
            SsrCatalogueId = result.SsrCatalogueId,
            SsrCode        = result.SsrCode,
            Label          = result.Label,
            Category       = result.Category,
            IsActive       = result.IsActive
        };
    }
}
