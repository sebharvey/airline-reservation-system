using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Template.TemplateApi.Domain.ExternalServices;
using ReservationSystem.Template.TemplateApi.Domain.ValueObjects;
using ReservationSystem.Template.TemplateApi.Infrastructure.ExternalServices.Models;
using ReservationSystem.Shared.Common.Json;
using System.Net.Http.Json;

namespace ReservationSystem.Template.TemplateApi.Infrastructure.ExternalServices;

/// <summary>
/// HTTP implementation of <see cref="ICurrencyExchangeClient"/>.
///
/// Calls the exchange rate REST API and maps the raw provider response into
/// the <see cref="ExchangeRate"/> domain value object. Provider-specific
/// JSON shapes are isolated inside this class; nothing above Infrastructure
/// ever sees them.
///
/// Data flow:
///   HTTP GET /latest?base={from}&symbols={to}&access_key={key}
///   → <see cref="ExchangeRateApiResponse"/> (raw provider JSON)
///   → <see cref="ExchangeRate"/> domain value object
/// </summary>
public sealed class CurrencyExchangeClient : ICurrencyExchangeClient
{
    private readonly HttpClient _httpClient;
    private readonly ExchangeRateClientOptions _options;
    private readonly ILogger<CurrencyExchangeClient> _logger;

    public CurrencyExchangeClient(
        HttpClient httpClient,
        IOptions<ExchangeRateClientOptions> options,
        ILogger<CurrencyExchangeClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExchangeRate?> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        var from = fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();

        var requestUri = BuildRequestUri(from, to);

        _logger.LogDebug("Calling exchange rate API: {Uri}", requestUri);

        ExchangeRateApiResponse? response;

        try
        {
            response = await _httpClient.GetFromJsonAsync<ExchangeRateApiResponse>(
                requestUri,
                SharedJsonOptions.CamelCase,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Exchange rate API request failed for {From} → {To}", from, to);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Exchange rate API request timed out for {From} → {To}", from, to);
            return null;
        }

        if (response is null || !response.Success)
        {
            _logger.LogWarning(
                "Exchange rate API returned an unsuccessful response for {From} → {To}",
                from, to);
            return null;
        }

        if (!response.Rates.TryGetValue(to, out var rate))
        {
            _logger.LogWarning("Currency {To} not present in exchange rate API response", to);
            return null;
        }

        var fetchedAt = DateTimeOffset.FromUnixTimeSeconds(response.Timestamp);

        return new ExchangeRate(from, to, rate, fetchedAt);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private string BuildRequestUri(string from, string to) =>
        $"{_options.BaseUrl.TrimEnd('/')}/latest?base={from}&symbols={to}&access_key={_options.ApiKey}";
}
