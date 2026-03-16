using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.ExternalServices;
using ReservationSystem.Template.TemplateApi.Domain.ValueObjects;

namespace ReservationSystem.Template.TemplateApi.Application.GetExchangeRate;

/// <summary>
/// Handles the <see cref="GetExchangeRateQuery"/>.
/// Orchestrates the external currency client; contains no HTTP or serialisation concerns.
/// </summary>
public sealed class GetExchangeRateHandler
{
    private readonly ICurrencyExchangeClient _currencyExchangeClient;
    private readonly ILogger<GetExchangeRateHandler> _logger;

    public GetExchangeRateHandler(
        ICurrencyExchangeClient currencyExchangeClient,
        ILogger<GetExchangeRateHandler> logger)
    {
        _currencyExchangeClient = currencyExchangeClient;
        _logger = logger;
    }

    public async Task<ExchangeRate?> HandleAsync(
        GetExchangeRateQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching exchange rate {From} → {To}",
            query.FromCurrency,
            query.ToCurrency);

        var rate = await _currencyExchangeClient.GetExchangeRateAsync(
            query.FromCurrency,
            query.ToCurrency,
            cancellationToken);

        if (rate is null)
            _logger.LogWarning(
                "Exchange rate not found for {From} → {To}",
                query.FromCurrency,
                query.ToCurrency);

        return rate;
    }
}
