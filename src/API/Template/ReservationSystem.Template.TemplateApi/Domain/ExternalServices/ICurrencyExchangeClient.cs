using ReservationSystem.Template.TemplateApi.Domain.ValueObjects;

namespace ReservationSystem.Template.TemplateApi.Domain.ExternalServices;

/// <summary>
/// Port (interface) for retrieving live currency exchange rates from a third-party provider.
/// Defined in Domain so the Application layer can depend on it without taking a dependency
/// on Infrastructure. The HTTP implementation lives in
/// Infrastructure/ExternalServices and is registered via DI at startup.
/// </summary>
public interface ICurrencyExchangeClient
{
    /// <summary>
    /// Fetches the current exchange rate between two ISO 4217 currency codes.
    /// Returns <c>null</c> when the currency pair is not supported by the provider.
    /// </summary>
    Task<ExchangeRate?> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);
}
