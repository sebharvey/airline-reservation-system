using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.UpdateSsrOption;

public sealed class UpdateSsrOptionHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public UpdateSsrOptionHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<SsrOptionDetailDto?> HandleAsync(UpdateSsrOptionCommand command, CancellationToken cancellationToken)
    {
        var result = await _orderServiceClient.UpdateSsrOptionAsync(command.SsrCode, command.Label, command.Category, cancellationToken);
        if (result is null) return null;

        return new SsrOptionDetailDto(result.SsrCatalogueId, result.SsrCode, result.Label, result.Category, result.IsActive);
    }
}
