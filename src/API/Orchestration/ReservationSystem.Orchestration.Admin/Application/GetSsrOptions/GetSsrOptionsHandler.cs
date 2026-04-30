using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.GetSsrOptions;

public sealed class GetSsrOptionsHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public GetSsrOptionsHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<SsrOptionListResponse> HandleAsync(GetSsrOptionsQuery query, CancellationToken cancellationToken)
    {
        var msResult = await _orderServiceClient.GetSsrOptionsAsync(cancellationToken);

        return new SsrOptionListResponse
        {
            SsrOptions = msResult.SsrOptions
                .Select(o => new SsrOptionSummary
                {
                    SsrCode  = o.SsrCode,
                    Label    = o.Label,
                    Category = o.Category
                })
                .ToList()
                .AsReadOnly()
        };
    }
}
