namespace ReservationSystem.Template.TemplateApi.Infrastructure.ExternalServices;

/// <summary>
/// Strongly-typed options for the currency exchange rate API client.
/// Bound from the "ExchangeRateApi" section of application configuration at startup.
///
/// In Azure, set the following Application Settings:
///   ExchangeRateApi__BaseUrl
///   ExchangeRateApi__ApiKey
///   ExchangeRateApi__TimeoutSeconds
/// </summary>
public sealed class ExchangeRateClientOptions
{
    public const string SectionName = "ExchangeRateApi";

    /// <summary>Base URL of the exchange rate provider (e.g. "https://api.exchangerate.host").</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>API key used to authenticate requests to the provider.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>HTTP request timeout in seconds. Defaults to 10.</summary>
    public int TimeoutSeconds { get; init; } = 10;
}
