using ReservationSystem.Microservices.Bags.Application.CreateBagPolicy;
using ReservationSystem.Microservices.Bags.Application.CreateBagPricing;
using ReservationSystem.Microservices.Bags.Application.UpdateBagPolicy;
using ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;
using ReservationSystem.Microservices.Bags.Models.Requests;
using ReservationSystem.Microservices.Bags.Models.Responses;

namespace ReservationSystem.Microservices.Bags.Models.Mappers;

public static class BagMapper
{
    // ── BagPolicy: Request → Command ─────────────────────────────────────────

    public static CreateBagPolicyCommand ToCommand(CreateBagPolicyRequest request) =>
        new(
            CabinCode: request.CabinCode,
            FreeBagsIncluded: request.FreeBagsIncluded,
            MaxWeightKgPerBag: request.MaxWeightKgPerBag);

    public static UpdateBagPolicyCommand ToCommand(Guid policyId, UpdateBagPolicyRequest request) =>
        new(
            PolicyId: policyId,
            FreeBagsIncluded: request.FreeBagsIncluded,
            MaxWeightKgPerBag: request.MaxWeightKgPerBag,
            IsActive: request.IsActive);

    // ── BagPolicy: Domain → Response ─────────────────────────────────────────

    public static BagPolicyResponse ToResponse(Domain.Entities.BagPolicy policy) =>
        new()
        {
            PolicyId = policy.PolicyId,
            CabinCode = policy.CabinCode,
            FreeBagsIncluded = policy.FreeBagsIncluded,
            MaxWeightKgPerBag = policy.MaxWeightKgPerBag,
            IsActive = policy.IsActive,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };

    public static IReadOnlyList<BagPolicyResponse> ToResponse(IEnumerable<Domain.Entities.BagPolicy> policies) =>
        policies.Select(ToResponse).ToList().AsReadOnly();

    // ── BagPricing: Request → Command ────────────────────────────────────────

    public static CreateBagPricingCommand ToCommand(CreateBagPricingRequest request) =>
        new(
            BagSequence: request.BagSequence,
            CurrencyCode: request.CurrencyCode,
            Price: request.Price,
            ValidFrom: request.ValidFrom,
            ValidTo: request.ValidTo);

    public static UpdateBagPricingCommand ToCommand(Guid pricingId, UpdateBagPricingRequest request) =>
        new(
            PricingId: pricingId,
            Price: request.Price,
            ValidFrom: request.ValidFrom,
            ValidTo: request.ValidTo,
            IsActive: request.IsActive);

    // ── BagPricing: Domain → Response ────────────────────────────────────────

    public static BagPricingResponse ToResponse(Domain.Entities.BagPricing pricing) =>
        new()
        {
            PricingId = pricing.PricingId,
            BagSequence = pricing.BagSequence,
            CurrencyCode = pricing.CurrencyCode,
            Price = pricing.Price,
            IsActive = pricing.IsActive,
            ValidFrom = pricing.ValidFrom,
            ValidTo = pricing.ValidTo,
            CreatedAt = pricing.CreatedAt,
            UpdatedAt = pricing.UpdatedAt
        };

    public static IReadOnlyList<BagPricingResponse> ToResponse(IEnumerable<Domain.Entities.BagPricing> pricings) =>
        pricings.Select(ToResponse).ToList().AsReadOnly();

    // ── BagOffer: Helpers ────────────────────────────────────────────────────

    public static string GetBagDescription(int bagSequence) => bagSequence switch
    {
        1 => "1st additional checked bag",
        2 => "2nd additional checked bag",
        99 => "3rd additional checked bag and beyond",
        _ => $"Additional checked bag (sequence {bagSequence})"
    };
}
