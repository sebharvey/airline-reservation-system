using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Application.GetStoredOffer;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Microservices.Offer.Domain.Services;

namespace ReservationSystem.Microservices.Offer.Application.RepriceStoredOffer;

/// <summary>
/// Re-prices a stored offer inventory entry against the current FareRule and live
/// cabin occupancy, copies TaxLines from the FareRule into each StoredOfferItem,
/// and marks the entry as validated.
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

        // 3. Fetch the live FlightInventory to get current cabin occupancy for dynamic pricing
        var inventory = await _repository.GetInventoryByIdAsync(entry.InventoryId, ct);

        // 4. Re-price each offer item against its FareRule and current occupancy
        var repricedItems = new List<StoredOfferItem>();
        foreach (var item in entry.Offers)
        {
            var fareRule = await _repository.GetFareRuleByIdAsync(item.FareRuleId, ct);

            // Derive updated fare amounts from the current FareRule + live cabin occupancy
            decimal baseFareAmount        = item.BaseFareAmount;
            decimal taxAmount             = item.TaxAmount;
            decimal totalAmount           = item.TotalAmount;
            bool isRefundable             = item.IsRefundable;
            bool isChangeable             = item.IsChangeable;
            decimal changeFeeAmount       = item.ChangeFeeAmount;
            decimal cancellationFeeAmount = item.CancellationFeeAmount;
            int? pointsPrice              = item.PointsPrice;
            decimal? pointsTaxes          = item.PointsTaxes;
            IReadOnlyList<TaxLineItem>? taxLines = item.TaxLines;

            if (fareRule is not null)
            {
                var cabin = inventory?.Cabins.FirstOrDefault(c => c.CabinCode == item.CabinCode);
                var occupancyRatio = cabin is not null
                    ? FarePricer.ComputeOccupancyRatio(cabin)
                    : 0.0;

                baseFareAmount        = FarePricer.ComputeDynamicPrice(fareRule.MinAmount, fareRule.MaxAmount, occupancyRatio);
                taxAmount             = fareRule.GetTotalTaxAmount();
                totalAmount           = baseFareAmount + taxAmount;
                isRefundable          = fareRule.IsRefundable;
                isChangeable          = fareRule.IsChangeable;
                changeFeeAmount       = fareRule.ChangeFeeAmount;
                cancellationFeeAmount = fareRule.CancellationFeeAmount;
                pointsPrice           = FarePricer.ComputeDynamicPoints(fareRule.MinPoints, fareRule.MaxPoints, occupancyRatio);
                pointsTaxes           = fareRule.PointsTaxes;

                if (!string.IsNullOrEmpty(fareRule.TaxLines))
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

                _logger.LogDebug(
                    "Reprice: OfferId {OfferId} CabinCode {CabinCode} — occupancy {Ratio:P0}, baseFare {Old} → {New}",
                    item.OfferId, item.CabinCode, occupancyRatio, item.BaseFareAmount, baseFareAmount);
            }
            else
            {
                _logger.LogWarning("Reprice: FareRule {FareRuleId} not found for OfferId {OfferId} — retaining stored amounts",
                    item.FareRuleId, item.OfferId);
            }

            repricedItems.Add(new StoredOfferItem
            {
                OfferId               = item.OfferId,
                FareRuleId            = item.FareRuleId,
                CabinCode             = item.CabinCode,
                FareBasisCode         = item.FareBasisCode,
                FareFamily            = item.FareFamily,
                CurrencyCode          = item.CurrencyCode,
                BaseFareAmount        = baseFareAmount,
                TaxAmount             = taxAmount,
                TotalAmount           = totalAmount,
                IsRefundable          = isRefundable,
                IsChangeable          = isChangeable,
                ChangeFeeAmount       = changeFeeAmount,
                CancellationFeeAmount = cancellationFeeAmount,
                PointsPrice           = pointsPrice,
                PointsTaxes           = pointsTaxes,
                SeatsAvailable        = item.SeatsAvailable,
                BookingType           = item.BookingType,
                TaxLines              = taxLines
            });
        }

        // 5. Rebuild FaresInfo: replace the matched entry (Validated = true), leave others untouched
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

        // 6. Persist the updated FaresInfo
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
