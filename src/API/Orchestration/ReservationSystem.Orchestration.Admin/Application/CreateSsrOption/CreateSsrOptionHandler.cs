using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.CreateSsrOption;

public sealed class CreateSsrOptionHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public CreateSsrOptionHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<SsrOptionResponse> HandleAsync(CreateSsrOptionCommand command, CancellationToken cancellationToken)
    {
        var body = new
        {
            ssrCode  = command.SsrCode.ToUpperInvariant(),
            label    = command.Label,
            category = command.Category
        };

        var result = await _orderServiceClient.CreateSsrOptionAsync(body, cancellationToken);

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
