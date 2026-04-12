using ReservationSystem.Microservices.Offer.Domain.Entities;

namespace ReservationSystem.Microservices.Offer.Models.Mappers;

/// <summary>Maps FareRule domain entities → API response objects.</summary>
public static class FareRuleMapper
{
    /// <summary>Maps domain FareRule → anonymous response object.</summary>
    public static object ToResponse(FareRule r)
    {
        return new
        {
            fareRuleId = r.FareRuleId,
            ruleType = r.RuleType,
            flightNumber = r.FlightNumber,
            fareBasisCode = r.FareBasisCode,
            fareFamily = r.FareFamily,
            cabinCode = r.CabinCode,
            bookingClass = r.BookingClass,
            currencyCode = r.CurrencyCode,
            minAmount = r.MinAmount,
            maxAmount = r.MaxAmount,
            minPoints = r.MinPoints,
            maxPoints = r.MaxPoints,
            pointsTaxes = r.PointsTaxes,
            taxLines = r.TaxLines != null
                ? System.Text.Json.JsonSerializer.Deserialize<object[]>(r.TaxLines)
                : null,
            isRefundable = r.IsRefundable,
            isChangeable = r.IsChangeable,
            changeFeeAmount = r.ChangeFeeAmount,
            cancellationFeeAmount = r.CancellationFeeAmount,
            validFrom = r.ValidFrom?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            validTo = r.ValidTo?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            createdAt = r.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            updatedAt = r.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    /// <summary>Maps a list of domain FareRules → response objects.</summary>
    public static IEnumerable<object> ToResponseList(IReadOnlyList<FareRule> rules)
    {
        return rules.Select(ToResponse);
    }
}
