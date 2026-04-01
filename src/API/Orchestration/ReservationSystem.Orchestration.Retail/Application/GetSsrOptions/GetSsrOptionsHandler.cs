using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetSsrOptions;

public sealed class GetSsrOptionsHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public GetSsrOptionsHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<GetSsrOptionsResponse> HandleAsync(GetSsrOptionsQuery query, CancellationToken cancellationToken)
    {
        var result = await _orderServiceClient.GetSsrOptionsAsync(query.CabinCode, query.FlightNumbers, cancellationToken);

        var options = result.SsrOptions
            .Select(o => new SsrOptionDto(o.SsrCode, o.Label, o.Category))
            .ToList()
            .AsReadOnly();

        return new GetSsrOptionsResponse(options);
    }
}
