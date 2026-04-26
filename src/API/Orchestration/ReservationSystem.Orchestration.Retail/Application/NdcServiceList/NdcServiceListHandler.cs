using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Retail.Application.NdcServiceList;

public sealed record NdcServiceListResult(
    IReadOnlyList<NdcSsrServiceItem> Services,
    OfferDetailDto? OfferDetail);

/// <summary>
/// A single SSR service item normalised for NDC ServiceListRS serialisation.
/// </summary>
public sealed record NdcSsrServiceItem(
    string SsrCode,
    string Label,
    string Category);

/// <summary>
/// Handles POST /v1/ndc/ServiceList.
///
/// Fetches active SSR options from the SSR Catalogue via the Order microservice.
/// When an OfferRefID is provided in the request the stored offer is resolved to
/// derive the operating cabin class and flight number, which are passed as filters
/// to the SSR catalogue query so that only contextually relevant services are
/// returned (e.g. meal options vary by cabin).
/// </summary>
public sealed class NdcServiceListHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;

    public NdcServiceListHandler(
        OrderServiceClient orderServiceClient,
        OfferServiceClient offerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
    }

    public async Task<NdcServiceListResult> HandleAsync(
        NdcServiceListCommand command,
        CancellationToken cancellationToken)
    {
        string? cabinCode    = null;
        string? flightNumber = null;
        OfferDetailDto? offerDetail = null;

        // Resolve flight context from the stored offer when an OfferRefID was supplied.
        if (command.OfferRefId.HasValue)
        {
            offerDetail = await _offerServiceClient.GetOfferAsync(
                command.OfferRefId.Value, cancellationToken: cancellationToken);

            if (offerDetail is not null)
            {
                var offerItem = offerDetail.Offers.FirstOrDefault();
                if (offerItem is not null)
                    cabinCode = offerItem.CabinCode;

                flightNumber = offerDetail.FlightNumber;
            }
        }

        // An explicit NDC cabin code in the request takes precedence over the offer-derived value.
        if (!string.IsNullOrWhiteSpace(command.NdcCabinCode))
            cabinCode = MapNdcCabinToInternal(command.NdcCabinCode);

        var ssrResult = await _orderServiceClient.GetSsrOptionsAsync(
            cabinCode, flightNumber, cancellationToken);

        var services = ssrResult.SsrOptions
            .Select(o => new NdcSsrServiceItem(o.SsrCode, o.Label, o.Category))
            .ToList();

        return new NdcServiceListResult(services, offerDetail);
    }

    /// <summary>
    /// Maps NDC cabin type codes (M/W/C/F) to the internal single-char cabin codes (Y/W/J/F).
    /// </summary>
    private static string MapNdcCabinToInternal(string ndcCode) => ndcCode.ToUpperInvariant() switch
    {
        "M" => "Y",
        "W" => "W",
        "C" => "J",
        "F" => "F",
        _   => ndcCode
    };
}
