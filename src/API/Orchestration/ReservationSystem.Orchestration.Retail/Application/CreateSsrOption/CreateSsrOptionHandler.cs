using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.CreateSsrOption;

public sealed class CreateSsrOptionHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public CreateSsrOptionHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<SsrOptionDetailDto?> HandleAsync(CreateSsrOptionCommand command, CancellationToken cancellationToken)
    {
        var result = await _orderServiceClient.CreateSsrOptionAsync(command.SsrCode, command.Label, command.Category, cancellationToken);
        if (result is null) return null;

        return new SsrOptionDetailDto(result.SsrCatalogueId, result.SsrCode, result.Label, result.Category, result.IsActive);
    }
}
