namespace ReservationSystem.Template.TemplateApi.Application.UseCases.GetExchangeRate;

/// <summary>
/// Query to retrieve a live exchange rate between two ISO 4217 currency codes.
/// Immutable record — all properties are set at creation and cannot be mutated.
/// </summary>
public sealed record GetExchangeRateQuery(string FromCurrency, string ToCurrency);
