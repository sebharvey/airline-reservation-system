using System.Text.Json.Serialization;

namespace ReservationSystem.Template.TemplateApi.Infrastructure.ExternalServices.Models;

/// <summary>
/// Raw JSON response model from the exchange rate API provider.
/// This is an infrastructure-layer model — it must never leak into the
/// Application or Domain layers. It is mapped to the <c>ExchangeRate</c>
/// domain value object inside <c>CurrencyExchangeClient</c>.
/// </summary>
internal sealed class ExchangeRateApiResponse
{
    /// <summary>Whether the API call succeeded.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>Unix timestamp at which rates were last updated by the provider.</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>The base currency used for the rate quote.</summary>
    [JsonPropertyName("base")]
    public string Base { get; init; } = string.Empty;

    /// <summary>Map of target currency code → exchange rate relative to <see cref="Base"/>.</summary>
    [JsonPropertyName("rates")]
    public Dictionary<string, decimal> Rates { get; init; } = [];
}
