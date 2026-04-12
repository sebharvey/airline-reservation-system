using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.RepriceStoredOffer;

/// <summary>
/// Reads the FareRule.TaxLines for each offer item in the matching inventory entry,
/// writes them into the stored FaresInfo JSON, and marks the entry as validated.
/// </summary>
public sealed record RepriceStoredOfferResult(
    Guid StoredOfferId,
    Guid SessionId,
    Guid InventoryId,
    bool Validated,
    IReadOnlyList<StoredOfferItem> Offers);

public sealed class RepriceStoredOfferHandler
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

    private readonly IOfferRepository _repository;
    private readonly ILogger<RepriceStoredOfferHandler> _logger;

    public RepriceStoredOfferHandler(IOfferRepository repository, ILogger<RepriceStoredOfferHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RepriceStoredOfferResult?> HandleAsync(RepriceStoredOfferCommand command, CancellationToken ct = default)
    {
        // 1. Retrieve the stored offer
        var offer = command.SessionId.HasValue
            ? await _repository.GetStoredOfferBySessionAndOfferIdAsync(command.SessionId.Value, command.OfferId, ct)
            : await _repository.GetStoredOfferByOfferIdAsync(command.OfferId, ct);

        if (offer is null)
        {
            _logger.LogWarning("Reprice: StoredOffer not found for OfferId {OfferId}", command.OfferId);
            return null;
        }

        if (offer.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Reprice: StoredOffer for OfferId {OfferId} has expired", command.OfferId);
            throw new OfferGoneException($"Offer {command.OfferId} has expired. Customer must re-search.");
        }

        var faresInfo = offer.GetFaresInfo();

        // 2. Find the inventory entry that contains the requested offerId
        var entry = faresInfo.Inventories
            .FirstOrDefault(e => e.Offers.Any(o => o.OfferId == command.OfferId));

        if (entry is null)
        {
            _logger.LogWarning("Reprice: OfferId {OfferId} not found in FaresInfo of StoredOffer {StoredOfferId}",
                command.OfferId, offer.StoredOfferId);
            return null;
        }

        // 3. Fetch FareRule for each offer item and copy in the tax lines
        var repricedItems = new List<StoredOfferItem>();
        foreach (var item in entry.Offers)
        {
            IReadOnlyList<TaxLineItem>? taxLines = null;

            var fareRule = await _repository.GetFareRuleByIdAsync(item.FareRuleId, ct);
            if (fareRule is not null && !string.IsNullOrEmpty(fareRule.TaxLines))
            {
                try
                {
                    taxLines = System.Text.Json.JsonSerializer.Deserialize<List<TaxLineItem>>(
                        fareRule.TaxLines, JsonOpts);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reprice: Failed to deserialise TaxLines for FareRule {FareRuleId}", item.FareRuleId);
                }
            }

            repricedItems.Add(new StoredOfferItem
            {
                OfferId               = item.OfferId,
                FareRuleId            = item.FareRuleId,
                CabinCode             = item.CabinCode,
                FareBasisCode         = item.FareBasisCode,
                FareFamily            = item.FareFamily,
                CurrencyCode          = item.CurrencyCode,
                BaseFareAmount        = item.BaseFareAmount,
                TaxAmount             = item.TaxAmount,
                TotalAmount           = item.TotalAmount,
                IsRefundable          = item.IsRefundable,
                IsChangeable          = item.IsChangeable,
                ChangeFeeAmount       = item.ChangeFeeAmount,
                CancellationFeeAmount = item.CancellationFeeAmount,
                PointsPrice           = item.PointsPrice,
                PointsTaxes           = item.PointsTaxes,
                SeatsAvailable        = item.SeatsAvailable,
                BookingType           = item.BookingType,
                TaxLines              = taxLines
            });
        }

        // 4. Rebuild the FaresInfo: replace the matched entry (set Validated = true),
        //    leave all other inventory entries untouched.
        var repricedEntry = new StoredOfferInventoryEntry
        {
            InventoryId = entry.InventoryId,
            Validated   = true,
            Offers      = repricedItems
        };

        var updatedInventories = faresInfo.Inventories
            .Select(e => e.InventoryId == entry.InventoryId ? repricedEntry : e)
            .ToList();

        var updatedFaresInfo = new StoredOfferFaresInfo { Inventories = updatedInventories };
        var updatedJson = System.Text.Json.JsonSerializer.Serialize(updatedFaresInfo, JsonOpts);

        // 5. Persist the updated FaresInfo
        await _repository.UpdateStoredOfferFaresInfoAsync(offer.StoredOfferId, updatedJson, ct);

        _logger.LogInformation(
            "Repriced StoredOffer {StoredOfferId} inventory entry {InventoryId} — Validated = true",
            offer.StoredOfferId, entry.InventoryId);

        return new RepriceStoredOfferResult(
            offer.StoredOfferId,
            offer.SessionId,
            entry.InventoryId,
            Validated: true,
            Offers: repricedItems);
    }
}
